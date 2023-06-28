using GithubSearch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GithubAPI
{
    public class Request
    {
        public string Topic { get; set; } = string.Empty;
        public int Size { get; set; }
        public int Stars { get; set; }
        public int Forks { get; set; }
        public int pageSize { get; set; }
        public int pageNum { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            Request other = obj as Request;
            bool eq = true;
            if (this.Topic != other.Topic) eq = false;
            if(this.Size != other.Size) eq = false;
            if(this.Forks != other.Forks) eq = false;
            if(this.pageSize != other.pageSize) eq = false;
            if(other.pageNum != other.pageNum) eq = false;

            return eq;
        }
        public override int GetHashCode()
        {
            return Size + Stars + Forks + pageSize + pageNum + Topic.GetHashCode();
        }
    }
    public class ConcurrentCache
    {
        private static ConcurrentDictionary<Request, List<Repositorium>> cache=new ConcurrentDictionary<Request, List<Repositorium>>();

        public static void UpdateValue(Request key,List<Repositorium> newValue)
        {
            cache.AddOrUpdate(key, newValue,(k,oldValue)=>newValue);
        }

        public static void RemoveKey(Request key)
        {
            cache.TryRemove(key,out _);
        }

        public static bool ReturunIfExists(Request key,out List<Repositorium> repos)
        {
            return cache.TryGetValue(key, out repos);
        }

        public static bool TryAdd(Request key,List<Repositorium> r)
        {
           return cache.TryAdd(key, r);
        }
    }
}
