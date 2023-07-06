// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using P42.Utils;

namespace Microsoft.Datasync.Client.Table
{
    /// <summary>
    /// The JSON constants for handling pages of items.
    /// </summary>
    internal static class Page
    {
        internal const string JsonCountProperty = "count";
        internal const string JsonItemsProperty = "items";
        internal const string JsonNextLinkProperty = "nextLink";
        internal const string OdataCountProperty = "@odata.count";
        internal const string OdataItemsProperty = "value";
        internal const string OdataNextLinkProperty = "@odata.nextLink";
        internal const string OdataContext = "@odata.context";
        internal const string OnPremResponse = "onPremResponse";
    }

    /// <summary>
    /// The model for the response from a query operation.
    /// </summary>
    public class Page<T> where T : notnull
    {
        /// <summary>
        /// The items in a page.
        /// </summary>
        public IEnumerable<T> Items { get; set; }

        /// <summary>
        /// The number of items that would be returned by the query,
        /// if not for paging.
        /// </summary>
        public long? Count { get; set; }

        /// <summary>
        /// The Uri to the nexty page in the result set.
        /// </summary>
        public Uri NextLink { get; set; }


    }

    public class OnPremResponse : IJsonReadable
    {
        const string StatusCodeKey = "responseStatuCode";
        const string ReasonKey = "responseReason";

        public int StatusCode { get; private set; }

        public string Reason { get; private set; }

        public static OnPremResponse CreateFrom(JsonReader reader)
        {
            reader.Read();

            if (reader.TokenType == JsonToken.None || reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidDataException($"Unexpected JSON token [{reader.TokenType}] [{reader.ValueType}] [{reader.Value}]");
            }

            return new OnPremResponse(reader);
        }

        private OnPremResponse(JsonReader reader)
            => PropertiesFrom(reader);

        public void PropertiesFrom(JsonReader reader)
        {
            if (reader.TokenType == JsonToken.StartObject)
                reader.Read();

            while (reader.TokenType == JsonToken.PropertyName)
            {
                var property = reader.Value as string;
                try
                {
                    if (!ReadJsonProperty(reader, property))  // we could implement property.ReplaceSafeCharacters() here but let's not slow things down since $type is (at this time) the only property that would benefit
                    {
                        //throw new InvalidDataContractException("Unexpected JSON Property [" + property + "] for class [" + GetType() + "].");
                        Serilog.Log.Warning("Unexpected JSON READER Property [" + property + "] for class [" + GetType() + "].");
                        reader.Read();
                    }
                    reader.Read();
                }
                catch (Exception e)
                {
                    Serilog.Log.Error(e, $"{GetType()}.PropertiesFrom(reader) : ");
                }
            }
            if (reader.TokenType != JsonToken.EndObject)
                //throw new InvalidDataContractException("Unexpected JsonToken [" + reader.TokenType + "][" + reader.Value + "][" + reader.Path + "]");
                Serilog.Log.Error("Unexpected JsonToken [" + reader.TokenType + "][" + reader.Value + "][" + reader.Path + "]");

        }

        bool ReadJsonProperty(JsonReader reader, string propertyName)
        {
            switch (propertyName)
            {
                case StatusCodeKey:
                    StatusCode = JsonExtensions.ReadInt(reader);
                    return true;
                case ReasonKey:
                    Reason = JsonExtensions.ReadString(reader);
                    return true;
                default:
                    return false;
            }

        }
    }

    public class JsonReadablePage<T> : Page<T> where T : notnull, IBaseModel, new()
    {
        public string Context { get; private set; }

        public OnPremResponse OnPremResponse { get; private set; }

        public JsonReadablePage(JsonReader reader)
        {
            PropertiesFrom(reader);
            reader.Close();

            System.Diagnostics.Debug.WriteLine($"JsonReadablePage.ctr:  GARBAGE COLLECTION START");
            var stopwatch = Stopwatch.StartNew();
            System.GC.Collect();
            stopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"JsonReadablePage.ctr :  GARBAGE COLLECTION END [{stopwatch.ElapsedMilliseconds}]");
        }

        public void PropertiesFrom(JsonReader reader)
        {
            reader.Read();

            if (reader.TokenType == JsonToken.StartArray)
                reader.Read();

            if (reader.TokenType == JsonToken.StartObject)
                reader.Read();

            while (reader.TokenType == JsonToken.PropertyName)
            {
                var property = reader.Value as string;
                try
                {
                    if (!ReadJsonProperty(reader, property))  // we could implement property.ReplaceSafeCharacters() here but let's not slow things down since $type is (at this time) the only property that would benefit
                    {
                        //throw new InvalidDataContractException("Unexpected JSON Property [" + property + "] for class [" + GetType() + "].");
                        Serilog.Log.Warning("Unexpected JSON READER Property [" + property + "] for class [" + GetType() + "].");
                        reader.Read();
                    }
                    reader.Read();
                }
                catch (Exception e)
                {
                    Serilog.Log.Error(e, $"{GetType()}.PropertiesFrom(reader) : ");
                }
            }
            if (reader.TokenType != JsonToken.EndObject)
                //throw new InvalidDataContractException("Unexpected JsonToken [" + reader.TokenType + "][" + reader.Value + "][" + reader.Path + "]");
                Serilog.Log.Error("Unexpected JsonToken [" + reader.TokenType + "][" + reader.Value + "][" + reader.Path + "]");

        }

        bool ReadJsonProperty(JsonReader reader, string propertyName)
        {
            switch (propertyName)
            {
                case Page.OdataItemsProperty:
                case Page.JsonItemsProperty:
                    Items = JsonExtensions.ReadIJsonReadableList<T>(reader);
                    return true;
                case Page.OdataCountProperty:
                case Page.JsonCountProperty:
                    Count = JsonExtensions.ReadLong(reader);
                    return true;
                case Page.OdataNextLinkProperty:
                case Page.JsonNextLinkProperty:
                    var url = JsonExtensions.ReadString(reader);
                    if (!string.IsNullOrWhiteSpace(url))
                        NextLink = new Uri(url);
                    return true;
                case Page.OdataContext:
                    Context = JsonExtensions.ReadString(reader);
                    return true;
                case Page.OnPremResponse:
                    OnPremResponse = OnPremResponse.CreateFrom(reader);
                    return true;
                default:
                    return false;
            }
        }
    }
}