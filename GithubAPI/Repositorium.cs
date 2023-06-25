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

        public async Task GetRepositoriums(string topic)
        {
            HttpClient client= new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");

            var url = $"https://api.github.com/search/repositories?q=topic:{topic}";
            
           // _ = Task.Run(async () =>
            await Task.Run(async () =>
            {
                Console.WriteLine("trying to enter try");
                try
                {
                    var response = await client.GetAsync(url);
                    Console.WriteLine("respones workds");
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine("Ensure works");
                    var content = await response.Content.ReadAsStringAsync();
                    var repos = JsonConvert.DeserializeObject<dynamic>(content).items;
                    Console.WriteLine("heyo");
                    //Console.WriteLine(repos[0]);
                    foreach (var r in repos)
                    {
                        var newRepo = new Repositorium
                        {
                            Name = r.name,
                            Description = r.description,
                            Forks = r.forks_count,
                            Size = r.size,
                            Stars = r.stargazers_count,
                        };
                        repositoriumSubject.OnNext(newRepo);
                    }
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
        }

        public IDisposable Subscribe(IObserver<Repositorium> observer)
        {
            return repositoriumSubject/*.ObserveOn(this.scheduler)*/.Subscribe(observer);
        }
    }

    public class RepositoriumObserver : IObserver<Repositorium>
    {
        private readonly string name;
        private string reply = string.Empty;
        private readonly HttpListenerContext context;
        private readonly IScheduler scheduler;

        public RepositoriumObserver(string name,HttpListenerContext c)
        {
            this.name = name;
            this.context = c;
            scheduler = new EventLoopScheduler();
        }
        public void OnNext(Repositorium repo)
        {
            Console.WriteLine($"{name}: {repo.Name}");
            this.reply += $"Name:{repo.Name}\nDescription:{repo.Description}\nSize:{repo.Size}\nStars:{repo.Stars}\n" +
                $"Forks:{repo.Forks}\n\n";
        }
        public void OnError(Exception ex)
        {
            Console.WriteLine("sucker");
        }
        public void OnCompleted()
        {
            HTTPServers.Response(200, context, reply.Length, reply);
            Console.WriteLine("Completed");
        }
    }

}
