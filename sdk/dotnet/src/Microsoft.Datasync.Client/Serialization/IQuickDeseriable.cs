using System;
namespace Microsoft.Datasync.Client.Serialization
{
    public interface IQuickDeseriable 
	{
		bool TrySetProperty(string key, object value);
	}
}

