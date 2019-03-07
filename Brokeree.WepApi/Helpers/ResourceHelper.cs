using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace Brokeree.WepApi.Helpers
{
    public class ResourceHelper
    {
        public static List<KeyValuePair<string, object>> Get()
        {
            MemoryCache _dataCache = MemoryCache.Default;
            try
            {
                var result = new List<KeyValuePair<string,object>>();
                result.AddRange(_dataCache);
                return result;
            }
            catch
            {
                return null;
            }
        }

        public static object Get(string pKey)
        {
            MemoryCache _dataCache = MemoryCache.Default;
            try
            {
                return _dataCache.Get(pKey);
            }
            catch
            {
                return null;
            }
        }

        public static object SetOrGet(string pKey, object obj, int KeepDataInMin = 60)
        {
            MemoryCache _dataCache = MemoryCache.Default;
            try
            {
                var policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = KeepDataInMin == 0 ? ObjectCache.InfiniteAbsoluteExpiration : DateTimeOffset.UtcNow.AddMinutes(KeepDataInMin)
                };
                return _dataCache.AddOrGetExisting(pKey, obj, policy);
            }
            catch(Exception e)
            {
                return null;
            }
        }

        public static bool Set(string pKey, object obj, int KeepDataInMin = 60)
        {
            MemoryCache _dataCache = MemoryCache.Default;
            try
            {
                var policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = KeepDataInMin == 0 ? ObjectCache.InfiniteAbsoluteExpiration : DateTimeOffset.UtcNow.AddMinutes(KeepDataInMin)
                };
                return _dataCache.Add(pKey, obj, policy);
            }
            catch
            {
                return false;
            }
        }

        public static bool Update(string pKey, object obj)
        {
            MemoryCache _dataCache = MemoryCache.Default;
            try
            {
                if (Remove(pKey))
                {
                    return _dataCache.Add(pKey, obj, new CacheItemPolicy());
                }
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }


        public static bool Remove(string key)
        {
            MemoryCache _dataCache = MemoryCache.Default;
            if (_dataCache.Contains(key))
            {
                if (_dataCache.Remove(key) == null)
                {
                    return false;
                }

                return CheckIsNull(key);          
            }
            else return false;
        }

        public static bool CheckIsNull(string pKey)
        {
            return Get(pKey) == null;
        }
    }
}