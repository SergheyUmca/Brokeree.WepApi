using Brokeree.WepApi.Factory.Models;
using Brokeree.WepApi.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Brokeree.WepApi.Factory
{
    public class ResourceFileStorage : IResourceManager
    {
        private string Source = "";

        public ResourceFileStorage(string pSource)
        {
            Source = pSource;
        }


        public Dictionary<string,string> Get()
        {
            var result = new Dictionary<string, string> ();
            try
            {
                var getFile = File.ReadAllLines(Source);
                foreach (var str in getFile)
                {
                    var pair = str.Split();
                    if(pair.Length == 2)
                    {
                        result.Add(pair[0], pair[1]);
                    }
                }
            }
            catch(Exception e)
            {

            }

            return result;
        }

        public string Get(string pKey)
        {
            var result = string.Empty;
            try
            {
                var getFile = File.ReadAllLines(Source);
                foreach (var str in getFile)
                {
                    var pair = str.Split();
                    if (pair.Length == 2)
                    {
                        if (pair[0].Equals(pKey))
                        {
                            result = pair[1];

                            return result;
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }

            return result;
        }


        public bool Remove(string pKey)
        {
            var result = new List<string>() ;
            try
            {
                var getFile = File.ReadAllLines(Source);
                foreach (var str in getFile)
                {
                    var pair = str.Split();
                    if (pair.Length == 2)
                    {
                        if (pair[0].Equals(pKey))
                        {
                            continue;
                        }
                    }
                    result.Add(str);
                }

                var saveFile = File.CreateText(Source);
                foreach ( var str in result)
                {
                    saveFile.WriteLine(str);
                }
                saveFile.Close();
               
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
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