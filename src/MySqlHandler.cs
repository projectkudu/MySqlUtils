﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MySqlUtils
{
    public class MySqlHandler : HttpTaskAsyncHandler
    {
        private Lazy<MySqlInfo> _sqlInfo = new Lazy<MySqlInfo>(MySqlInfo.Create);
        protected readonly Tracer _tracer;

        public MySqlHandler(Tracer tracer)
        {
            _tracer = tracer;
        }

        public override bool IsReusable
        {
            get { return true; }
        }

        public string BasePath
        {
            get { return _sqlInfo.Value.BasePath; }
        }

        public string Server
        {
            get { return _sqlInfo.Value.Server; }
        }

        public int Port
        {
            get { return _sqlInfo.Value.Port; }
        }

        public string Database
        {
            get { return _sqlInfo.Value.Database; }
        }

        public string UserID
        {
            get { return _sqlInfo.Value.UserID; }
        }

        public string Password
        {
            get { return _sqlInfo.Value.Password; }
        }

        public void Trace(string message)
        {
            _tracer.Trace(message);
        }

        public void Trace(object obj)
        {
            _tracer.Trace(obj);
        }

        public void Trace(string format, params object[] args)
        {
            _tracer.Trace(format, args);
        }

        public override Task ProcessRequestAsync(HttpContext context)
        {
            var dict = new Dictionary<string, object>
            {
                { "Verb", context.Request.HttpMethod },
                { "RawUrl", context.Request.RawUrl },
                { "Url", context.Request.Url },
                { "Body", new StreamReader(context.Request.InputStream).ReadToEnd() },
            };

            var strb = new StringBuilder();
            strb.AppendLine("<!DOCTYPE html>");
            strb.AppendLine("<html xmlns=\"http://www.w3.org/1999/xhtml\">");
            strb.AppendLine("<head>");
            strb.AppendLine("    <title></title>");
            strb.AppendLine("    <style>");
            strb.AppendLine("    table, th, td {");
            strb.AppendLine("        border: 1px solid black;");
            strb.AppendLine("        border-collapse: collapse;");
            strb.AppendLine("    }");
            strb.AppendLine("    th, td {");
            strb.AppendLine("        padding: 5px;");
            strb.AppendLine("        text-align: left;");
            strb.AppendLine("    }");
            strb.AppendLine("    </style>");
            strb.AppendLine("</head>");
            strb.AppendLine("<body>");
            strb.AppendLine("<table>");
            strb.AppendLine("   <tr>");
            strb.AppendLine("       <th>key</th>");
            strb.AppendLine("       <th>value</th>");
            strb.AppendLine("   </tr>");
            foreach (var pair in dict)
            {
                strb.AppendLine("   <tr>");
                strb.AppendLine(String.Format("       <td>{0}</td>", pair.Key));
                strb.AppendLine(String.Format("       <td>{0}</td>", pair.Value));
                strb.AppendLine("   </tr>");
            }
            foreach (var key in context.Request.ServerVariables.AllKeys)
            {
                strb.AppendLine("   <tr>");
                strb.AppendLine(String.Format("       <td>{0}</td>", key));
                strb.AppendLine(String.Format("       <td>{0}</td>", context.Request.ServerVariables[key]));
                strb.AppendLine("   </tr>");
            }
            strb.AppendLine("</table>");
            strb.AppendLine("</body>");
            strb.AppendLine("</html>");

            context.Response.Write(strb.ToString());
            context.Response.End();

            return Task.FromResult(true);
        }

        class MySqlInfo
        {
            public static MySqlInfo Create()
            {
                string connectionString = null;
                if (Environment.GetEnvironmentVariable("WEBSITE_MYSQL_ENABLED") == "1")
                {
                    connectionString = File.ReadAllLines(@"D:\home\data\mysql\MYSQLCONNSTR_localdb.txt")[0];
                }
                else
                {
                    connectionString = GetMySqlConnectionStringEnv();
                }

                if (String.IsNullOrEmpty(connectionString))
                {
                    if (Utils.IsAzure)
                    {
                        throw new InvalidOperationException("Cannot find MySql connection string!");
                    }

                    connectionString = File.ReadAllLines(@"c:\temp\mysql\MYSQLCONNSTR_localdb.txt")[0];
                }

                var dict = connectionString.Split(';').Select(pair => pair.Split('=')).ToDictionary(arr => arr[0], arr => arr[1]);
                var info = new MySqlInfo();
                var ds = dict["Data Source"].Split(':');
                info.Server = ds[0];
                info.Port = ds.Length > 1 ? int.Parse(ds[1]) : 3306;
                info.Database = dict["Database"];
                info.UserID = dict["User Id"];
                info.Password = dict["Password"];

                var dir = new DirectoryInfo(Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\mysql"));
                if (dir.Exists)
                {
                    info.BasePath = dir.GetDirectories()
                        .Where(d => File.Exists(Path.Combine(d.FullName, "bin", "mysqldump.exe")))
                        .Select(d => d.FullName).LastOrDefault();
                }

                if (String.IsNullOrEmpty(info.BasePath))
                {
                    info.BasePath = @"c:\mysql\5.7.9.0.win32";
                }

                return info;
            }

            private MySqlInfo()
            {
            }

            public string Server
            {
                get; private set;
            }
            public int Port
            {
                get; private set;
            }
            public string Database
            {
                get; private set;
            }
            public string UserID
            {
                get; private set;
            }

            public string Password
            {
                get; private set;
            }

            public string BasePath
            {
                get; private set;
            }

            private static string GetMySqlConnectionStringEnv()
            {
                foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
                {
                    if (((string)env.Key).StartsWith("MYSQLCONNSTR_", StringComparison.OrdinalIgnoreCase))
                    {
                        var connectionString = (string)env.Value;
                        if (!String.IsNullOrEmpty(connectionString))
                        {
                            return connectionString;
                        }
                    }
                }

                return null;
            }
        }
    }
}