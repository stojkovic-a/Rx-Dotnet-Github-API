using GithubAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace GithubSearch
{
    public class Repositorium
    {
        public string Name = string.Empty;
        public string Description=string.Empty;
        public int Size;
        public int Stars;
        public int Forks;
    }

    public class RepositoriumStream : IObservable<Repositorium>
    {
        private readonly Subject<Repositorium> repositoriumSubject;
        private readonly IScheduler scheduler;
        public RepositoriumStream()
        {
            repositoriumSubject = new Subject<Repositorium>();
            scheduler = new EventLoopScheduler();
        }

        public async Task GetRepositoriums(Request r,HTTPServers server )
        {
            //List<Repositorium> reposPom = new List<Repositorium>();
            //if (ConcurrentCache.ReturunIfExists(r, out reposPom))
            //{
            //    server.FoundInCacheAtomicIncrement();
            //    foreach (var rp in reposPom)
            //    {
            //        repositoriumSubject.OnNext(rp);
            //        Console.WriteLine("hello");
            //    }
            //    Console.WriteLine("pre");
            //    repositoriumSubject.OnCompleted();
            //    Console.WriteLine("psole");

            //}
            //else
            //{

                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
                string pageUrl = string.Empty;
                if (r.pageNum == -1)
                {
                    pageUrl = $"&per_page=100&page=1";

                }
                else
                {
                    pageUrl = $"&per_page={r.pageSize}&page={r.pageNum}";
                }
                var url = $"https://api.github.com/search/repositories?q=topic:{r.Topic}" + pageUrl;


                await Task.Run(async () =>
                {
                    try
                    {
                        var response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        var content = await response.Content.ReadAsStringAsync();
                        var repos = JsonConvert.DeserializeObject<dynamic>(content).items;
                        //Console.WriteLine(repos[0]);
                        List<Repositorium> listOfRepos = new List<Repositorium>();
                        foreach (var re in repos)
                        {
                            var newRepo = new Repositorium
                            {
                                Name = re.name,
                                Description = re.description,
                                Forks = re.forks_count,
                                Size = re.size,
                                Stars = re.stargazers_count,
                            };
                            listOfRepos.Add(newRepo);
                            repositoriumSubject.OnNext(newRepo);
                        }
                        ConcurrentCache.TryAdd(r, listOfRepos);
                        repositoriumSubject.OnCompleted();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Something went wrong");
                        Console.WriteLine(ex);
                        Console.WriteLine(ex.Message);
                        repositoriumSubject.OnError(ex);
                    }
                });
           // }
        }

        public IDisposable Subscribe(IObserver<Repositorium> observer)
        {
            return repositoriumSubject.Subscribe(observer);
        }
    }

    public class RepositoriumObserver : IObserver<Repositorium>
    {
        private readonly string name;
        private string reply = string.Empty;
        private readonly HttpListenerContext context;
        private readonly IScheduler scheduler;
        private readonly HTTPServers server;

        public RepositoriumObserver(string name,HttpListenerContext c,HTTPServers server)
        {
            this.name = name;
            this.context = c;
            scheduler = new EventLoopScheduler();
            this.server = server;
        }
        public void OnNext(Repositorium repo)
        {
            //Console.WriteLine($"{name}: {repo.Name}");
            this.reply += $"Name:{repo.Name}\nDescription:{repo.Description}\nSize:{repo.Size}\nStars:{repo.Stars}\n" +
                $"Forks:{repo.Forks}\n\n";
        }
        public void OnError(Exception ex)
        {
            Console.WriteLine("Doslo je do greske");
        }
        public void OnCompleted()
        {
            Console.WriteLine("COmpleted");
            if (reply == string.Empty)
            {
                HTTPServers.Response(404, context);
                this.server.NotFoundAtomicIncrement();
            }
            else
            {
                HTTPServers.Response(200, context, reply.Length, reply);
            }
                Task t1 = HTTPServers.Loggs(true, true, context.Request);
        }
    }

}
