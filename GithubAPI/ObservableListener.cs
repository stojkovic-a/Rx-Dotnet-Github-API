using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace GithubSearch
{
    public class ObservableListener:IObservable<HttpListenerContext>
    {
        private readonly IScheduler scheduler = ThreadPoolScheduler.Instance;
        private readonly short Port;
        private readonly HttpListener listener;
        private string address;

        public ObservableListener(string address,short port = 5050)
        {
            this.address = address;
            this.Port= port;
            this.listener = new HttpListener();
            this.listener.Prefixes.Add("http://" + address + ":" + this.Port.ToString() + "/");
        }

        public IDisposable Subscribe(IObserver<HttpListenerContext> observer)
        {
            this.listener.Start();
            try
            {
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    scheduler.Schedule(() => { observer.OnNext(context); });
                }
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
            finally
            {
                observer.OnCompleted();
            }

            return Disposable.Create(() =>
            {
                listener.Close();
            });
        }
    }
}
