using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace GithubSearch
{
    public class HTTPServers
    {
        private readonly int pageSize = 5;

        private object fileWriteLock = new object();
        private readonly string loggFileName = "Loggs.txt";

        private string address;
        private short Port;
        private HttpListener listener;

        volatile private int numOfRequests;
        volatile private int numBadRequests;
        volatile private int numNotFoundReq;
        volatile private int numFoundInCache;
        volatile private int numFoundInDirectory;

        public HTTPServers(string address,short port = 5050)
        {
            this.Port = port;
            this.address = address;
            this.listener = new HttpListener();
            this.listener.Prefixes.Add("http://" + address + ":" + this.Port.ToString() + "/");

            numOfRequests = 0;
            numBadRequests = 0;
            numNotFoundReq = 0;
            numFoundInCache = 0;
            numFoundInDirectory = 0;
        }

        public void Start(System.Threading.CancellationToken token)
        {
            this.listener.Start();
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                Interlocked.Increment(ref numOfRequests);
                Task t1 = HandleRequest(context);

                if (token.IsCancellationRequested)
                {
                    break;
                }
            }
            Console.WriteLine("Server not listening anymore");
            Report();
        }

        public async Task HandleRequest(HttpListenerContext context)
        {
            string topic = string.Empty;
            int minSize = 0;
            int minStars = 0;
            int minForks = 0;
            int pageination = 0;
            int pageNum = 0;
            bool valid = true;
            if (context.Request.HttpMethod != "GET") { valid = false; }

            if (context.Request.AcceptTypes != null)
            {
                bool validPom = false;
                foreach(var type in context.Request.AcceptTypes)
                {
                    if (type.Contains("application/json") || type.Contains("*/*"))
                    {
                        validPom = true;
                        break;
                    }
                }
                valid = valid && validPom;
            }
            else
            {
                valid = false;
            }

            if (context.Request.RawUrl == null || context.Request.RawUrl == "/"||context.Request.QueryString.ToString()==null|| context.Request.QueryString.ToString()==string.Empty)
            {
                valid = false;
            }
            else
            {
                var qs = context.Request.QueryString;
                var parsed = qs;//HttpUtility.ParseQueryString(qs!);
                if (parsed["topic"] != null && parsed["topic"]!=string.Empty)
                {
                    topic = parsed["topic"];
                    minSize = Int32.Parse(parsed["size"] ?? "0");
                    minStars = Int32.Parse(parsed["stars"] ?? "0");
                    minForks = Int32.Parse(parsed["forks"] ?? "0");
                    pageination = Int32.Parse(parsed["pging"] ?? "0");
                    if (pageination == 1)
                    {
                        pageNum = Int32.Parse(parsed["pg"] ?? "1");
                    }
                    else
                    {
                        pageNum = 0;
                    }
                }
                else
                {
                    valid = false;
                }

            }

            if (valid)
            {
                var repoStream = new RepositoriumStream();

                var observer1 = new RepositoriumObserver("Observer 1",context);

                var filteredStream = repoStream.Where(r => r.Size > minSize && r.Forks > minForks && r.Stars > minStars);
                if (pageination == 1) {
                    filteredStream=filteredStream.Skip((pageNum-1)*pageSize).Take(pageSize);
                }

                var subscription1 = filteredStream
                   // .SubscribeOn(Scheduler.Default)
                    .Subscribe(observer1);

                 await repoStream.GetRepositoriums(topic);
                subscription1.Dispose();
            }
            else
            {
                Task t1 = Loggs(false, false, context.Request );
                Response(400, context);
                Interlocked.Increment(ref numBadRequests);
            }
        }

        public static void Response(int responseCode, HttpListenerContext context, int len = 0, string content = null)
        {
            string body = string.Empty;
            var response = context.Response;
            response.StatusCode = responseCode;

            if (responseCode == 400)
            {
                response.ContentType = "text/html";
                body = @"<html>
                    <head><title>Bad Request</title></head>
                    <body>Bad request was made</body>
                                </html>";
                try
                {
                    response.OutputStream.Write(Encoding.ASCII.GetBytes(body));
                    response.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                    return;
                }
                return;
            }
            else
            if (responseCode == 404)
            {
                response.ContentType = "text/html";
                body = @"<html>
                    <head><title>Not Found</title></head>
                    <body>Gif Not Found</body>
                                </html>";
                try
                {
                    response.OutputStream.Write(Encoding.ASCII.GetBytes(body));
                    response.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                    return;
                }
                return;
            }
            else
            if (responseCode == 200)
            {
                response.ContentType = "text/plain";
                response.ContentLength64 = len;
                try
                {
                    response.OutputStream.Write(Encoding.ASCII.GetBytes(content));
                    response.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                    return;
                }
                return;
            }

        }
        public void Report()
        {
            Console.WriteLine($"{this.numOfRequests} requests were received");
            Console.WriteLine($"{this.numBadRequests} were bad requests");
            Console.WriteLine($"{this.numNotFoundReq} were not found");
            Console.WriteLine($"Of {this.numOfRequests - this.numBadRequests - this.numNotFoundReq} found requests," +
                $" {this.numFoundInCache} were found in cache and {this.numFoundInDirectory} were found in directorty");
        }

        private async Task Loggs(bool valid, bool successful,HttpListenerRequest request)
        {
            string text = string.Empty;
            if (!valid)
            {
                text = @"--Invalid request received at " + DateTime.Now.ToString() + "\n";
            }
            else
            {
                text = @"--Unkown error at" + DateTime.Now.ToString() + "\n";
            }
            text += $"Request:{request.HttpMethod} {request.RawUrl} {request.ProtocolVersion}\n";
            text += "Accept: ";
            if (request.AcceptTypes != null)
            {
                foreach (var types in request.AcceptTypes)
                {
                    text += types + " ";
                }
            }
            text += "\n\n";


            lock (fileWriteLock)
                WriteFile(text);
        }

        private void WriteFile(string text)
        {
            if (!File.Exists(this.loggFileName))
            {
                File.Create(this.loggFileName);
                TextWriter tw = new StreamWriter(this.loggFileName);
                tw.Write(text);
                tw.Close();
            }
            else if (File.Exists(this.loggFileName))
            {
                TextWriter tw = new StreamWriter(this.loggFileName, true);
                tw.Write(text);
                tw.Close();
            }
        }
    }
}
