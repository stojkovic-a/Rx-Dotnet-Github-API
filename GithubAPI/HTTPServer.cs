using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GithubSearch
{
    public class HTTPServers
    {
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
            bool valid = true;
            if (context.Request.HttpMethod != "GET") { valid = false; }

            if (context.Request.AcceptTypes != null)
            {
                bool validPom = false;
                foreach(var type in context.Request.AcceptTypes)
                {
                    if (type.Contains("image/gif") || type.Contains("*/*"))
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

            if (context.Request.RawUrl == null || context.Request.RawUrl == "/")
            {
                valid = false;
            }
            else
            {
               topic = context.Request.RawUrl;
               topic = topic.Remove(0, 1);
            }

            if (valid)
            {
                //SendRequest to api
            }
            else
            {
               // Task t1 = Loggs(new object[] { false, false, false, false, context.Request });
               // Response(400, context);
                Interlocked.Increment(ref numBadRequests);
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
    }
}
