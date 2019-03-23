using System.Collections.Generic;

namespace Brokeree.WepApi.Models
{
    public class ResponseModel<Object> : ResponseModel
    {
        public Object ResponseObject { get; set; }  
    }

    public class ResponseModel
    {
        public string ResponseMessage { get; set; }
        public bool IsOk { get; set; }
        public string AppMethod { get; set; }
        public string Controller { get; set; }
    }

    public class GetAllResponseModel
    {
        public int Count { get; set; }
        public List<ResourceObject> Resources { get; set; }
    }

    public class UpdateRequestModel
    {
        public string ID { get; set; }
        public string Value { get; set; }
        public UpdateTypes UpdateType { get; set; }
        public int? StartIndex { get; set; }
        public int? Length { get; set; }
        public string OldSubstring { get; set; }
    }

    public enum UpdateTypes
    {
        RenewValue = 0,
        InsertIntoStart = 1,
        InsertIntoEnd = 2,
        InsertIntoCustom = 3,
        DeleteSubstring = 4,
        UpdateSubstring = 5
    }

    public class ResourceObject
    {
        public string ID { get; set; }
        public string Value { get; set; }
    }

    public class WebHookRequest : ResourceObject
    {
        public string appMethod { get; set; }
    }
}