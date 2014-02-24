﻿namespace BGGAPI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using BGGAPI.Thing.Comments;

    using RestSharp;

    /// <summary>
    /// The client.
    /// </summary>
    public class Client
    {
        /// <summary>
        /// The default serializer.
        /// </summary>
        private static readonly Func<object, string> DefaultSerializer = o => o.ToString();

        /// <summary>
        /// The type.
        /// </summary>
        private static readonly IDictionary<Type, Func<object, string>> TypeSerializers =
            new Dictionary<Type, Func<object, string>>
                {
                    { typeof(bool), o => (bool)o ? "1" : null },

                    // We list bool? explicitly as we want different behaviour in the false case from the
                    // simple bool serialization
                    { typeof(bool?), o => ((bool?)o).Value ? "1" : "0" },
                    { typeof(DateTime), o => ((DateTime)o).ToString("yy-MM-dd HH:mm:ss") },
                    { typeof(List<int>), o => string.Join(",", (List<int>)o) },
                };

        /// <summary>
        /// Requests information about a user's collection
        /// </summary>
        /// <param name="collectionRequest">Details of the request</param>
        /// <returns>Details of the user's collection</returns>
        public Collection.Collection GetCollection(Collection.Request collectionRequest)
        {
            if (string.IsNullOrEmpty(collectionRequest.UserName))
            {
                throw new ArgumentException("Null or empty username in collectionRequest");
            }

            return CallBGG<Collection.Collection>("collection", collectionRequest);
        }

        /// <summary>
        /// Requests information about specific BGG objects
        /// </summary>
        /// <param name="thingsRequest">Details of the request</param>
        /// <returns>Details on the requested objects</returns>
        public Thing.Return GetThings(Thing.Request thingsRequest)
        {
            if (thingsRequest.ID == null || !thingsRequest.ID.Any())
            {
                throw new ArgumentException("Null or empty list of IDs in thingsRequest");
            }

            return CallBGG<Thing.Return>("thing", thingsRequest);
        }

        /// <summary>
        /// Requests information about specific BGG objects
        /// </summary>
        /// <param name="thingsRequest">Details of the request</param>
        /// <returns>Details on the requested objects</returns>
        public Dictionary<int, List<Comment>> GetComments(Thing.Request thingsRequest)
        {
            if (thingsRequest.ID == null || !thingsRequest.ID.Any())
            {
                throw new ArgumentException("Null or empty list of IDs in thingsRequest");
            }

            // for each item in items give me all of the comments for the item.
            // return as Dictionary<id, List<Comments>>?
            // path: items[0..int.max].Comments.CommentsList[0..int.max]
            // simpler to only accept one id and just return those comments instea,d.

            var returnRequest = CallBGG<Return>("thing", thingsRequest);

            var comDictionary = new Dictionary<int, List<Comment>>();

            foreach (Item item in returnRequest.Items)
            {
                var comments = item.Comments.CommentList.Select(comment =>
                    new Comment
                    {
                        Rating = comment.Rating,
                        UserName = comment.UserName,
                        value = comment.value
                    }).ToList();
                comDictionary.Add(item.ID, comments);
            }

            return comDictionary;
        }


        /// <summary>
        /// Call Board Game Geek.
        /// </summary>
        /// <param name="resource">
        /// The resource.
        /// </param>
        /// <param name="request">
        /// The request.
        /// </param>
        /// <typeparam name="T">
        /// Unknown Unknown
        /// </typeparam>
        /// <returns>
        /// The <see cref="T"/>
        /// </returns>
        /// <exception cref="Exception">
        /// Unknown Exception
        /// </exception>
        private static T CallBGG<T>(string resource, object request) where T : new()
        {
            var client = new RestClient { BaseUrl = Constants.BoardGameUrl };

            // http://technet.weblineindia.com/web/basics-of-restsharp-in-dot-net/2/
            // client.CookieContainer = new System.Net.CookieContainer();

            var restRequest = new RestRequest { Resource = resource };
            foreach (var parameter in SerializeRequest(request))
            {
                restRequest.AddParameter(parameter.Key, parameter.Value);
            }

            var response = client.Execute<T>(restRequest);
            if (response.ErrorException != null)
            {
                throw response.ErrorException;
            }

            return response.Data;
        }

        /// <summary>
        /// The serialize request.
        /// </summary>
        /// <param name="request">
        /// The request.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable"/>.
        /// </returns>
        private static IEnumerable<KeyValuePair<string, string>> SerializeRequest(object request)
        {
            var parameters = new Dictionary<string, string>();

            foreach (var propertyInfo in request.GetType().GetProperties())
            {
                var value = propertyInfo.GetValue(request);
                if (value == null)
                {
                    continue;
                }

                Func<object, string> serializer;

                var propertyType = propertyInfo.PropertyType;
                if (!TypeSerializers.TryGetValue(propertyType, out serializer))
                {
                    // If this was a nullable type, look for a serializer for the underlying type
                    var underlyingType = Nullable.GetUnderlyingType(propertyType);
                    if (underlyingType != null)
                    {
                        TypeSerializers.TryGetValue(underlyingType, out serializer);
                    }
                }

                if (serializer == null)
                {
                    serializer = DefaultSerializer;
                }

                var serializedValue = serializer(value);
                if (serializedValue != null)
                {
                    parameters.Add(propertyInfo.Name.ToLower(), serializedValue);
                }
            }

            return parameters;
        }
    }
}
