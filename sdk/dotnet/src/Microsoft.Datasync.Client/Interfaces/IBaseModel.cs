using System;
using System.ComponentModel;

namespace Microsoft.Datasync.Client;

public interface IBaseModel : INotifyPropertyChanged, P42.Utils.IJsonReadable, Serialization.IQuickDeseriable
{
    string Id { get; set; }

    DateTimeOffset UpdatedAt { get; set; }

    DateTimeOffset CreatedAt { get; set; }

    bool Deleted { get; set; }

    string Version { set; get; }

    Newtonsoft.Json.Linq.JObject ToJObject();
}

