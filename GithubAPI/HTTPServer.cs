using GithubAPI;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        //ConcurrentCache cache = new ConcurrentCache();

        private readonly int defaultPageSize =7;

        private static object fileWriteLock = new object();
        private static readonly string loggFileName = "Loggs.txt";

        private string address;
        private short Port;
        private ObservableListener listener;

        volatile private int numOfRequests;
        volatile private int numBadRequests;
        volatile private int numNotFoundReq;
        volatile private int numFoundInCache;

        public HTTPServers(string address,short port = 5050)
        {
            this.Port = port;
            this.address = address;
            this.listener = new ObservableListener(address, port);

            numOfRequests = 0;
            numBadRequests = 0;
            numNotFoundReq = 0;
            numFoundInCache = 0;
        }

        public void Start(System.Threading.CancellationToken token)
        {
            var listenSub = listener
                .Do(async c =>
                {
                    await HandleRequest(c);
                })
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe();
        }

        public async Task HandleRequest(HttpListenerContext context)
        {
            Interlocked.Increment(ref numOfRequests);
            string topic = string.Empty;
            int minSize = 0;
            int minStars = 0;
            int minForks = 0;
            int pageination = 0;
            int pageNum = 0;
            int pageSizePom = 100;
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
                    if (!Int32.TryParse(parsed["size"] ?? "0", out minSize)) minSize = 0;
                    if (!Int32.TryParse(parsed["stars"] ?? "0", out minStars)) minStars = 0;
                    if(!Int32.TryParse(parsed["forks"] ?? "0",out minForks))minForks = 0;
                    if(! Int32.TryParse(parsed["pging"] ?? "0",out pageination))pageination=0;
                    if (pageination == 1)
                    {
                        pageSizePom = this.defaultPageSize;
                        if(! Int32.TryParse(parsed["pg"] ?? "1",out pageNum))pageNum=1;
                    }
                    else
                    {
                        pageNum = -1;
                        pageSizePom = 100;
                    }
                }
                else
                {
                    valid = false;
                }

            }

            if (valid)
            {
                Request request = new Request
                {
                    Topic = topic,
                    Size = minSize,
                    Stars = minStars,
                    Forks = minForks,
                    pageNum = pageNum,
                    pageSize = pageSizePom
                };
                var repoStream = new RepositoriumStream();

                var observer1 = new RepositoriumObserver("Observer 1",context,this);

                var filteredStream = repoStream.Where(r => r.Size > minSize && r.Forks > minForks && r.Stars > minStars);
                //if (pageination == 1) {
                //    filteredStream=filteredStream.Skip((pageNum-1)*pageSize).Take(pageSize);
                //}

                var subscription1 = filteredStream
                    .SubscribeOn(Scheduler.Default)//demonstrativno samo
                    .Subscribe(observer1);

                 await repoStream.GetRepositoriums(request,this);
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
                    <body>Corresponding Results Not Found</body>
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
                $" {this.numFoundInCache} were found in cache");
        }

        public static async Task Loggs(bool valid, bool successful,HttpListenerRequest request)
        {
            string text = string.Empty;
            if (!valid)
            {
                text = @"--Invalid request received at " + DateTime.Now.ToString() + "\n";
            }
            else if (valid && successful)
            {
                text = @"--Valid request received at " + DateTime.Now.ToString() + "\n";
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

        private static void WriteFile(string text)
        {
            if (!File.Exists(HTTPServers.loggFileName))
            {
                File.Create(HTTPServers.loggFileName);
                TextWriter tw = new StreamWriter(HTTPServers.loggFileName);
                tw.Write(text);
                tw.Close();
            }
            else if (File.Exists(HTTPServers.loggFileName))
            {
                TextWriter tw = new StreamWriter(HTTPServers.loggFileName, true);
                tw.Write(text);
                tw.Close();
            }
        }

        public void NotFoundAtomicIncrement()
        {
            Interlocked.Increment(ref this.numNotFoundReq);
        }
        public void FoundInCacheAtomicIncrement()
        {
            Interlocked.Increment(ref this.numFoundInCache);
        }
    }
}
