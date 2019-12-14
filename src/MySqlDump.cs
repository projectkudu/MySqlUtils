using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace MySqlUtils
{
    public class MySqlDump : MySqlHandler
    {
        private static string _resultFile = Utils.IsAzure ? @"d:\local\temp\mysqldump_temp.sql" : @"c:\temp\mysql\mysqldump_temp.sql";
        private static MySqlProcess _process;

        public MySqlDump(Tracer tracer)
            : base(tracer)
        {
        }

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            if (context.Request.RawUrl.IndexOf(Utils.GetPath("{0}/dump/status"), StringComparison.OrdinalIgnoreCase) == 0)
            {
                await HandleResponse(context);
                return;
            }

            var exe = Path.Combine(BasePath, "bin", "mysqldump.exe");
            // --single-transaction aquires a REPEATABLE READ lock for the time of the dump to ensure consistency
            // http://dev.mysql.com/doc/refman/5.6/en/mysqldump.html#option_mysqldump_single-transaction
            var arguments = string.Format(@"--single-transaction -u{0} -p{1} -h{2} --port={3} --result-file={4} {5}", UserID, Password, Server, Port, _resultFile, Database);
            Trace("\"{0}\" {1}", exe, arguments.Replace(Password, "*****"));
            
            MySqlProcess process;
            if (!TryStartSqlProcess(context, exe, arguments, out process))
            {
                return;
            }

            var processName = process.ProcessName;
            var processId = process.Id;

            _process = process;
            Trace("{0}:{1} started", processName, processId);

            if (context.Request.QueryString["async"] == "1")
            {
                await HandleResponse(context);
                return;
            }

            try
            {
                // wait for exit
                await process.WaitForExitAsync(Utils.MySqlTimeout);
            }
            catch (Exception ex)
            {
                _process = null;
                throw TerminateProcess(process, ex);
            }

            // to flush all output
            process.WaitForExit();

            await HandleResponse(context);
        }

        private async Task HandleResponse(HttpContext context)
        {
            var process = _process;
            if (process != null)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        if (process.StartTime.Add(Utils.MySqlTimeout) < DateTime.UtcNow)
                        {
                            throw new TimeoutException(String.Format("Execute sql process {0}:{1} has exceeded {2} timeout", process.ProcessName, process.Id, Utils.MySqlTimeout));
                        }
                    }
                    catch (Exception ex)
                    {
                        _process = null;
                        throw TerminateProcess(process, ex);
                    }

                    // return location
                    var location = new Uri(context.Request.Url, Utils.GetPath("{0}/dump/status"));
                    context.Response.Headers["Location"] = location.AbsoluteUri;
                    context.Response.StatusCode = 202;
                    return;
                }

                Trace("{0}:{1} exited with {2}.", process.ProcessName, process.Id, process.ExitCode);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(String.Format("{0}:{1} exit with {2}!  {3}", process.ProcessName, process.Id, process.ExitCode, process.Output));
                }
            }

            _process = null;
            var prefix = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            if (String.IsNullOrEmpty(prefix))
            {
                prefix = "mysqldump";
            }

            var fileName = String.Format("{0}-{1:yy-MM-dd-HH-mm-ss}.sql", prefix, DateTime.UtcNow);
            context.Response.ContentType = "text/plain";
            context.Response.Headers["Content-Disposition"] = "attachment; filename=" + fileName;
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            using (var fs = File.OpenRead(_resultFile))
            {
                await fs.CopyToAsync(context.Response.OutputStream);
            }
            context.Response.End();

            Utils.SafeExecute(() => File.Delete(_resultFile), _tracer);
        }

        private Exception TerminateProcess(MySqlProcess process, Exception ex)
        {
            process.Kill();

            Trace("{0}:{1} killed.", process.ProcessName, process.Id);

            return new InvalidOperationException(String.Format("Wait for process {0}:{1} failed with {2}", process.ProcessName, process.Id, ex), ex);
        }
    }
}