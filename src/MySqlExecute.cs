using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MySqlUtils
{
    public class MySqlExecute : MySqlHandler
    {
        public MySqlExecute(Tracer tracer)
            : base(tracer)
        {
        }

        public async override Task ProcessRequestAsync(HttpContext context)
        {
            var exe = Path.Combine(BasePath, "bin", "mysql.exe");
            var arguments = string.Format(@"-u{0} -p{1} -h{2} --port={3} {4}", UserID, Password, Server, Port, Database);
            Trace("\"{0}\" {1}", exe, arguments.Replace(Password, "*****"));

            MySqlProcess process;
            if (!TryStartSqlProcess(context, exe, arguments, out process))
            {
                return;
            }

            var processName = process.ProcessName;
            var processId = process.Id;

            Trace("{0}:{1} started", processName, processId);

            Func<CancellationToken, Task> executeSql = async (CancellationToken cancellationToken) =>
            {
                await context.Request.InputStream.CopyToAsync(process.Input, 4096, cancellationToken);
                await process.Input.FlushAsync(cancellationToken);

                var quitCmd = Encoding.UTF8.GetBytes("\n\\q\n");
                await process.Input.WriteAsync(quitCmd, 0, quitCmd.Length, cancellationToken);
                await process.Input.FlushAsync(cancellationToken);

                if (!cancellationToken.IsCancellationRequested)
                {
                    // to flush all output
                    process.WaitForExit();
                }
            };

            var cts = new CancellationTokenSource();
            var tasks = new[] { Task.Delay(Utils.MySqlTimeout, cts.Token), executeSql(cts.Token) };
            var task = await Task.WhenAny(tasks);

            // cancel all pending tasks
            cts.Cancel();

            // first task is Delay.  This implies timeout.
            if (task == tasks[0])
            {
                process.Kill();

                Trace("{0}:{1} killed.", processName, processId);

                throw new TimeoutException(String.Format("Execute sql process {0}:{1} has exceeded {2} timeout", processName, processId, Utils.MySqlTimeout));
            }

            // to ensure successful in executing query and process has exited
            await task;

            Trace("{0}:{1} exited with {2}.", processName, processId, process.ExitCode);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(String.Format("{0}:{1} exit with {2}!  {3}", processName, processId, process.ExitCode, process.Output));
            }

            context.Response.StatusCode = 200;
            context.Response.Write(process.Output);
            context.Response.End();
        }
    }
}