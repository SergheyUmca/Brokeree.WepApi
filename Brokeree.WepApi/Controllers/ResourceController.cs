using Brokeree.WepApi.Helpers;
using Brokeree.WepApi.Models;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Brokeree.WepApi.Controllers
{
    public class ResourceController : ApiController
    {
        private const string STORAGE_PREFIX = "RES_";
        private const string DEFAULT_VALUE = "AbBbbCDE";
        private const string WEBHOOK_URL = "https://reqres.in/api/webhook";
        private const string CONTROLLER_NAME = "Resource";
        private const int TRY_COUNT = 3;

        private Logger nLog = LogManager.GetLogger("ExceptionsLogger");


        [HttpGet]
        public ResponseModel<GetAllResponseModel> Get()
        {
            var result = new ResponseModel<GetAllResponseModel>() {
                ResponseMessage = "Successful",
                IsOk = true,
                AppMethod = "Get Resources",
                Controller = CONTROLLER_NAME
            };
            try
            {
               var getCache =  ResourceHelper.Get();
                var returnObject = new List<ResourceObject>();
                foreach (var elem  in getCache)
                {
                    if(elem.Key.StartsWith(STORAGE_PREFIX))
                    {
                        returnObject.Add(new ResourceObject
                        {
                            ID = elem.Key.Substring(STORAGE_PREFIX.Length),
                            Value = elem.Value.ToString()
                        });
                    }
                }
                if(returnObject != null)
                {
                    result.ResponseObject = new GetAllResponseModel
                    {
                        Count = returnObject.Count,
                        Resources = returnObject
                    };
                }
                else
                {
                    result.ResponseMessage = "Not Found no one resource";
                    result.IsOk = false;
                    return result;
                }
            }
            catch( Exception e)
            {
                result.ResponseMessage = e.Message;
                result.IsOk = false;
                return result;
            }
            finally
            {
                if (!result.IsOk)
                {
                    nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                       $"{result.Controller}/{result.AppMethod}: {result.ResponseMessage}"));
                }
            }
            return result;
        }

        [HttpGet]
        public ResponseModel<ResourceObject> Get(string ID, int? startIndex, int? length)
        {
            var result = new ResponseModel<ResourceObject>()
            {
                ResponseMessage = "Successful",
                IsOk = true,
                AppMethod = "Get Resource/Substring",
                Controller = CONTROLLER_NAME
            };

            bool needSubstring = (startIndex != null && startIndex >= 0) && (length != null && length > 0);
            var vKey = $"{STORAGE_PREFIX}{ID}";

            try
            {
                if(string.IsNullOrEmpty(ID))
                {
                    result.ResponseMessage = "ID can't be NULL or EMPTY";
                    result.IsOk = false;
                    return result;
                }

                var getResource = ResourceHelper.Get(vKey);
                if(getResource == null)
                {
                    result.ResponseMessage = $"Resource with id = {ID} Not Found ";
                    result.IsOk = false;
                    return result;
                }

                result.ResponseObject = new ResourceObject
                {
                    ID = ID,
                    Value = getResource.ToString()
                };

                if(needSubstring)
                {
                    var valueLength = result.ResponseObject.Value.Length;
                   if (length > valueLength || length + startIndex > valueLength)
                    {
                        result.ResponseMessage = "Wrong Substring length ,it is so big";
                        result.IsOk = false;
                        return result;
                    }

                   if(startIndex >= valueLength)
                    {
                        result.ResponseMessage = "Wrong startIndex";
                        result.IsOk = false;
                        return result;
                    }

                    result.ResponseObject.Value = result.ResponseObject.Value.Substring((int)startIndex, (int)length);
                }

            }
            catch (Exception e)
            {
                result.ResponseMessage = e.Message;
                result.IsOk = false;
                return result;    
            }
            finally
            {
                if (!result.IsOk)
                {
                    nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                       $"{result.Controller}/{result.AppMethod}: {result.ResponseMessage}"));
                }
            }
            return result;
        }

        [HttpGet]
        public ResponseModel<ResourceObject> GetDefault()
        {
            var result = new ResponseModel<ResourceObject>()
            {
                ResponseMessage = "Successful",
                IsOk = true,
                AppMethod = "Get Default Resource",
                Controller = CONTROLLER_NAME
            };
            try
            {
                result.ResponseObject = new ResourceObject()
                {
                    ID = Guid.NewGuid().ToString(),
                    Value = DEFAULT_VALUE
                };
               
            }
            catch (Exception e)
            {

                nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                    $"{result.Controller}/{result.AppMethod}: {result.ResponseMessage}"));

                return new ResponseModel<ResourceObject>
                {
                    IsOk = false,
                    ResponseMessage = e.Message
                };
            }
            return result;
        }

        [HttpPost]
        public ResponseModel Create(string Value, string ID)
        {
            var result = new ResponseModel()
            {
                ResponseMessage = "Successful",
                IsOk = true,
                AppMethod = "Create Resource",
                Controller = CONTROLLER_NAME
            };

            string vKey = STORAGE_PREFIX;
            try
            {
                vKey += string.IsNullOrEmpty(ID) ? Guid.NewGuid().ToString() : ID;

                var addResource = ResourceHelper.SetOrGet(vKey, Value, 0);
                if(addResource != null)
                {
                    result.ResponseMessage = $"Resource with ID = {ID} is already exist";
                    result.IsOk = false;
                    return result;   
                }
            }
            catch (Exception e)
            {

                result.IsOk = false;
                result.ResponseMessage = e.Message;
            }
            finally
            {
                if (!result.IsOk)
                {
                    nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                       $"{result.Controller}/{result.AppMethod}: {result.ResponseMessage}"));
                }
                else
                {
                    WebHoockCall(new WebHookRequest
                    {
                        ID = ID,
                        Value = Value,
                        appMethod = result.AppMethod
                    });
                }
            }
            return result;
        }

        [HttpPut]
        public ResponseModel Update(UpdateRequestModel requestModel)
        {
            var result = new ResponseModel()
            {
                ResponseMessage = "Successful",
                IsOk = true,
                AppMethod = $"Update Resource/Substring, UpdateType = {requestModel.UpdateType.ToString()}",
                Controller = CONTROLLER_NAME
            };

            if (string.IsNullOrEmpty(requestModel.ID))
            {
                result.ResponseMessage = "ID can't be NULL or EMPTY";
                result.IsOk = false;
                return result;
            }

            var vKey = $"{STORAGE_PREFIX}{requestModel.ID}";
            var vValue = string.Empty;

            try
            {
                if (requestModel.UpdateType == UpdateTypes.RenewValue)
                {
                   var renewValue = ResourceHelper.Update(vKey, requestModel.Value);
                    if(!renewValue)
                    {
                        result.ResponseMessage = $"Resource with id = { requestModel.ID } Not Found";
                        result.IsOk = false;
                        return result;
                    }
                    vValue = requestModel.Value;
                    return result;
                }
                else
                {
                    var getValue = ResourceHelper.Get(vKey)?.ToString();
                    if(getValue == null)
                    {
                            result.ResponseMessage = $"Resource with id = { requestModel.ID } Not Found";
                            result.IsOk = false;
                            return result;
                    }

                    var valueLength = getValue.Length;
                    if (requestModel.UpdateType == UpdateTypes.DeleteSubstring)
                    {
                        if (requestModel.Length > valueLength || requestModel.Length + requestModel.StartIndex > valueLength)
                        {
                            result.ResponseMessage = $"Wrong Substring length ,it is so big";
                            result.IsOk = false;
                            return result;
                        }
                    }

                    if (requestModel.UpdateType == UpdateTypes.DeleteSubstring || requestModel.UpdateType == UpdateTypes.InsertIntoCustom )
                    {
                        if (requestModel.StartIndex >= valueLength)
                        {
                            result.ResponseMessage = $"Wrong startIndex";
                            result.IsOk = false;
                            return result;
                        }
                    }
                       
                    switch (requestModel.UpdateType)
                    {
                        case UpdateTypes.InsertIntoStart :
                            {
                                getValue = $"{requestModel.Value}{getValue}";
                                break;
                            };
                        case UpdateTypes.InsertIntoEnd :
                            {
                                getValue = $"{getValue}{requestModel.Value}";
                                break;
                            };
                        case UpdateTypes.InsertIntoCustom :
                            {
                                getValue = getValue.Insert((int)requestModel.StartIndex, requestModel.Value);
                                break;
                            };
                        case UpdateTypes.DeleteSubstring :
                            {
                                getValue = getValue.Remove((int)requestModel.StartIndex, (int)requestModel.Length);
                                break;
                            };
                        case UpdateTypes.UpdateSubstring :
                            {
                                getValue = getValue.Replace(requestModel.OldSubstring, requestModel.Value);
                                break;
                            };
                        default:

                            {
                                result.ResponseMessage = $"Wrong UpdateTypes";
                                result.IsOk = false;
                                return result;
                            }
                    }

                    var renewValue = ResourceHelper.Update(vKey, getValue);
                    if (!renewValue)
                    {
                        result.ResponseMessage = $"Resource with id = { requestModel.ID } Not Found ";
                        result.IsOk = false;
                        return result;
                    }
                    vValue = getValue;
                    return result;
                }    
            }
            catch (Exception e)
            {
                result.ResponseMessage = e.Message;
                result.IsOk = false;
                return result;

            }
            finally
            {
                if(result.IsOk)
                {
                    WebHoockCall(new WebHookRequest
                    {
                        ID = requestModel.ID,
                        Value = vValue,
                        appMethod = result.AppMethod
                    });
                }
                else
                {
                    if (!result.IsOk)
                    {
                        nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                           $"{result.Controller}/{result.AppMethod}: {result.ResponseMessage}"));
                    }
                }
            }
        }
        
        [HttpDelete]
        public ResponseModel Delete(string ID)
        {
            var result = new ResponseModel()
            {
                ResponseMessage = "Successful",
                IsOk = true,
                AppMethod = "Delete Resource",
                Controller = CONTROLLER_NAME
            }; 
            try
            {
                if (string.IsNullOrEmpty(ID))
                {
                    result.ResponseMessage = "ID can't be NULL or EMPTY";
                    result.IsOk = false;
                    return result;
                }

                string vKey = $"{STORAGE_PREFIX}{ID}";

                var removeRes = ResourceHelper.Remove(vKey);
                if(!removeRes)
                {
                    result.ResponseMessage = "Resource not deleted, try again";
                    result.IsOk = false;
                    return result;
                }
            }
            catch (Exception e)
            {
                return new ResponseModel
                {
                    ResponseMessage = e.Message
                };
            }
            finally
            {
                if (result.IsOk)
                {
                    WebHoockCall(new WebHookRequest
                    {
                        ID = ID,
                        Value = null,
                        appMethod = result.AppMethod
                    });
                }
                else
                {
                    if (!result.IsOk)
                    {
                        nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                           $"{result.Controller}/{result.AppMethod}: {result.ResponseMessage}"));
                    }
                }
            }
            return result;
        }


        private async Task WebHoockCall (WebHookRequest requestModel)
        {
            var succesDelivery = false;
            var tryCount = TRY_COUNT;
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificatePolicy;
                var _client = new HttpClient { BaseAddress = new Uri(WEBHOOK_URL) };
                _client.DefaultRequestHeaders.Accept.Clear();
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

                string json = JsonConvert.SerializeObject(requestModel);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response =  _client.PutAsync(WEBHOOK_URL, content).Result;


                succesDelivery = response.StatusCode == HttpStatusCode.OK;
                if(succesDelivery)
                {
                    nLog.Log(new LogEventInfo(LogLevel.Info, "DeliveryLogger",
                           $"{CONTROLLER_NAME}/ WebHoockCall: Delivered is status {response.StatusCode}"));
                }

            }
            catch (HttpRequestException httpEx)
            {
                nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                           $"{CONTROLLER_NAME}/ WebHoockCall: {httpEx.Message}"));
            }
            catch(Exception e)
            {
                nLog.Log(new LogEventInfo(LogLevel.Error, "ExceptionsLogger",
                           $"{CONTROLLER_NAME}/ WebHoockCall: {e.Message}"));
                tryCount = 0;
            }
            finally
            {
                while (tryCount > 0 && !succesDelivery)
                {
                    ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificatePolicy;
                    var _client = new HttpClient { BaseAddress = new Uri(WEBHOOK_URL) };
                    _client.DefaultRequestHeaders.Accept.Clear();
                    _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

                    string json = JsonConvert.SerializeObject(requestModel);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = _client.PutAsync(WEBHOOK_URL, content).Result;

                    succesDelivery = response.StatusCode == HttpStatusCode.OK;
                    if (succesDelivery)
                    {
                        nLog.Log(new LogEventInfo(LogLevel.Info, "DeliveryLogger",
                               $"{CONTROLLER_NAME}/ WebHoockCall: Delivered try ({TRY_COUNT - tryCount}) is status {response.StatusCode}"));
                    }

                    tryCount--;
                }
            }


        }

        internal static bool TrustAllCertificatePolicy(object sender,
                                        X509Certificate certificate,
                                        X509Chain chain,
                                        System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}