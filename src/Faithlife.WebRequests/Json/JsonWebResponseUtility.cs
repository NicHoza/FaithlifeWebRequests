using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Faithlife.Utility;
using Faithlife.Json;
using Newtonsoft.Json;

namespace Faithlife.WebRequests.Json
{
	/// <summary>
	/// Utility methods for JSON and HttpResponseMessage.
	/// </summary>
	/// <remarks>The JSON in the response content must be represented using UTF-8.</remarks>
	public static class JsonWebResponseUtility
	{
		/// <summary>
		/// Returns true if the response content uses the JSON content type.
		/// </summary>
		/// <param name="response">The response.</param>
		/// <returns>True if the response content uses the JSON content type ("application/json") and the content is not empty.</returns>
		public static bool HasJson(this HttpResponseMessage response)
		{
			// Allow null ContentLength
			var hasJson = response.Content?.Headers.ContentLength != 0;
			var contentType = response.Content?.Headers.ContentType?.ToString();
			hasJson &= contentType is object && contentType.Length >= JsonWebServiceContent.JsonContentType.Length &&
				contentType.Trim().StartsWith(JsonWebServiceContent.JsonContentType, StringComparison.Ordinal);

			return hasJson;
		}

		/// <summary>
		/// Gets the JSON.
		/// </summary>
		/// <param name="response">The response.</param>
		/// <returns>The unverified JSON.</returns>
		/// <exception cref="WebServiceException">The response content does not use the JSON content type or the content is empty.</exception>
		public static async Task<string> GetJsonAsync(this HttpResponseMessage response)
		{
			if (!response.HasJson())
				throw await HttpResponseMessageUtility.CreateWebServiceExceptionWithContentPreviewAsync(response, "The response does not have JSON content.").ConfigureAwait(false);

			return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
		}

		/// <summary>
		/// Parses the JSON into an object of the specified type.
		/// </summary>
		/// <param name="response">The response.</param>
		/// <param name="type">The type.</param>
		/// <returns>An object of the specified type.</returns>
		/// <exception cref="WebServiceException">The response content does not use the JSON content type, or the content is empty,
		/// or the text is not valid JSON, or the JSON cannot be deserialized into the specified type.</exception>
		/// <remarks>Use JToken as the type to parse arbitrary JSON.</remarks>
		public static Task<object?> GetJsonAsAsync(this HttpResponseMessage response, Type type) => response.GetJsonAsAsync(type, null);

		/// <summary>
		/// Parses the JSON into an object of the specified type.
		/// </summary>
		/// <param name="response">The response.</param>
		/// <param name="type">The type.</param>
		/// <param name="jsonSettings">The JSON settings.</param>
		/// <returns>An object of the specified type.</returns>
		/// <exception cref="WebServiceException">The response content does not use the JSON content type, or the content is empty,
		/// or the text is not valid JSON, or the JSON cannot be deserialized into the specified type.</exception>
		/// <remarks>Use JToken as the type to parse arbitrary JSON.</remarks>
		public static async Task<object?> GetJsonAsAsync(this HttpResponseMessage response, Type type, JsonSettings? jsonSettings)
		{
			try
			{
				// StreamReader will throw an ArgumentException when Stream.CanRead is false. It's suspected that this
				// might happen if the HTTP request is canceled (via HttpClient.Timeout) before the response is read.
				var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
				if (!responseStream.CanRead)
					throw HttpResponseMessageUtility.CreateWebServiceException(response, "Response stream is not readable.");

				// parse JSON to desired value
				using var wrappingStream = new WrappingStream(responseStream, Ownership.None);
				using var reader = new StreamReader(wrappingStream);
				return JsonUtility.FromJsonTextReader(reader, type, jsonSettings);
			}
			catch (JsonReaderException x)
			{
				// JSON invalid
				throw HttpResponseMessageUtility.CreateWebServiceException(response, "Response is not valid JSON: " + x.Message, x);
			}
			catch (JsonSerializationException x)
			{
				// JSON can't be deserialized
				throw HttpResponseMessageUtility.CreateWebServiceException(response, "Response JSON could not be deserialized to {0}: {1}".FormatInvariant(type, x.Message), x);
			}
		}

		/// <summary>
		/// Parses the JSON into an object of the specified type.
		/// </summary>
		/// <typeparam name="T">The specified type.</typeparam>
		/// <param name="response">The response.</param>
		/// <returns>An object of the specified type.</returns>
		/// <remarks>Use JToken as the type to parse arbitrary JSON.</remarks>
		/// <exception cref="WebServiceException">The response content does not use the JSON content type, or the content is empty,
		/// or the text is not valid JSON, or the JSON cannot be deserialized into the specified type.</exception>
		public static Task<T> GetJsonAsAsync<T>(this HttpResponseMessage response) => response.GetJsonAsAsync<T>(null)!;

		/// <summary>
		/// Parses the JSON into an object of the specified type.
		/// </summary>
		/// <typeparam name="T">The specified type.</typeparam>
		/// <param name="response">The response.</param>
		/// <param name="jsonSettings">The JSON settings.</param>
		/// <returns>An object of the specified type.</returns>
		/// <exception cref="WebServiceException">The response content does not use the JSON content type, or the content is empty,
		/// or the text is not valid JSON, or the JSON cannot be deserialized into the specified type.</exception>
		public static async Task<T> GetJsonAsAsync<T>(this HttpResponseMessage response, JsonSettings? jsonSettings)
		{
			var result = await response.GetJsonAsAsync(typeof(T), jsonSettings).ConfigureAwait(false);
			return (T) result!;
		}
	}
}
