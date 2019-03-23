using System.Collections.Generic;

namespace Brokeree.WepApi.Factory.Models
{
    interface IResourceManager
    {
        Dictionary<string, string> Get();
        string Get(string pKey);
        object SetOrGet(string pKey, string obj);
        bool Set(string pKey, string obj);
        bool Update(string pKey, string obj);
        bool Remove(string key);
    }
}
