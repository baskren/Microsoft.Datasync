using System;
namespace Microsoft.Datasync.Client
{
	public static class QueryStringExtensions
	{
        public static long GetSkipValue(this string queryString)
        {
            queryString ??= string.Empty;

            var filterString = "filter=";
            var updatedQueryString = queryString;
            var index = 0;
            if (queryString.Contains(filterString))
            {
                index = queryString.IndexOf(filterString) + filterString.Length;
                updatedQueryString = queryString.Substring(index);
            }

            var parameters = updatedQueryString.Split('&');

            var oldSkip = 0L;

            foreach (var parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter))
                    continue;

                if (parameter.StartsWith(Query.OData.ODataOptions.Skip))
                {
                    var parts = parameter.Split('=');
                    var valueString = parts[1];
                    oldSkip = long.Parse(valueString);
                }
            }

            return oldSkip;
        }


        public static long CumalativeRecordsRequested(this string queryString)
		{
            queryString ??= string.Empty;

            var filterString = "filter=";
            var updatedQueryString = queryString;
            var index = 0;
            if (queryString.Contains(filterString))
            {
                index = queryString.IndexOf(filterString) + filterString.Length;
                updatedQueryString = queryString.Substring(index);
            }

            var parameters = updatedQueryString.Split('&');

            var oldTop = 0L;
            var oldSkip = 0L;

            foreach (var parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter))
                    continue;

                if (parameter.StartsWith(Query.OData.ODataOptions.Top))
                {
                    var parts = parameter.Split('=');
                    var valueString = parts[1];
                    oldTop = long.Parse(valueString);
                }
                else if (parameter.StartsWith(Query.OData.ODataOptions.Skip))
                {
                    var parts = parameter.Split('=');
                    var valueString = parts[1];
                    oldSkip = long.Parse(valueString);
                }
            }

            var newSkip = oldTop + oldSkip;
            return newSkip;
        }

		public static string AssurePaginateQueryString(this string queryString)
			=> PaginateQueryString(queryString, false);

		public static string NextPageQueryString(this string queryString)
			=> PaginateQueryString(queryString, true);

        static string PaginateQueryString(this string queryString, bool isNext)
		{
			queryString ??= string.Empty;
			queryString = queryString.TrimStart('?');


			System.Diagnostics.Debug.WriteLine($"QueryStringExtensions.PaginateQueryString : QueryString [{queryString}]");


			var filterString = "filter=";
			var updatedQueryString = queryString;
			var index = 0;
			if (queryString.Contains(filterString))
			{
				index = queryString.IndexOf(filterString) + filterString.Length;
                updatedQueryString = queryString.Substring(index);
			}

			var updatedParameters = new List<string>();
			var parameters = updatedQueryString.Split('&');

			var oldTop = 0L;
			var oldSkip = 0L;

			foreach (var parameter in parameters)
			{
				if (string.IsNullOrWhiteSpace(parameter))
					continue;

				if (parameter.StartsWith(Query.OData.ODataOptions.Top))
				{
					var parts = parameter.Split('=');
					var valueString = parts[1];
					oldTop = long.Parse(valueString);
				}
				else if (parameter.StartsWith(Query.OData.ODataOptions.Skip))
				{
                    var parts = parameter.Split('=');
                    var valueString = parts[1];
                    oldSkip = long.Parse(valueString);
                }
				else
					updatedParameters.Add(parameter);
			}

			var newSkip = oldTop + oldSkip;
			if (isNext && newSkip > 0)
				updatedParameters.Add($"{Query.OData.ODataOptions.Skip}={newSkip}");
            updatedParameters.Add($"{Query.OData.ODataOptions.Top}={Query.OData.ODataOptions.DefaultTopValue}");

			var result = '?' + queryString.Substring(0, index) + string.Join('&', updatedParameters);

            System.Diagnostics.Debug.WriteLine($"QueryStringExtensions.PaginateQueryString : result [{result}]");


            return result;
		}
	}
}

