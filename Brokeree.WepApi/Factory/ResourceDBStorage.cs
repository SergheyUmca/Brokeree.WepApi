using Brokeree.WepApi.Factory.Models;
using System;
using System.Collections.Generic;

namespace Brokeree.WepApi.Factory
{
    public class ResourceDBStorage : IResourceManager
    {

        public ResourceDBStorage ()
        {

        }

        public List<KeyValuePair<string, string>> Get()
        {
            throw new NotImplementedException();
        }

        public object Get(string pKey)
        {
            throw new NotImplementedException();
        }

        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public bool Set(string pKey, string obj)
        {
            throw new NotImplementedException();
        }

        public object SetOrGet(string pKey, string obj)
        {
            throw new NotImplementedException();
        }

        public bool Update(string pKey, string obj)
        {
            throw new NotImplementedException();
        }
    }
}