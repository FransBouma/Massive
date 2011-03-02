// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Massive.cs" company="">
//   
// </copyright>
// <summary>
//   Defines the ObjectExtensions type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Massive
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Data;
    using System.Data.Common;
    using System.Dynamic;
    using System.Linq;

    /// <summary>
    /// Object Extensions
    /// </summary>
    /// <remarks></remarks>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Extension method for adding in a bunch of parameters
        /// </summary>
        /// <param name="cmd">The command.</param>
        /// <param name="args">The arguments.</param>
        /// <remarks></remarks>
        public static void AddParams(this DbCommand cmd, object[] args)
        {
            foreach (var item in args)
            {
                AddParam(cmd, item);
            }
        }

        /// <summary>
        /// Extension for adding single parameter
        /// </summary>
        /// <param name="cmd">The CMD.</param>
        /// <param name="item">The item.</param>
        /// <remarks></remarks>
        public static void AddParam(this DbCommand cmd, object item)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = string.Format("@{0}", cmd.Parameters.Count);
            if (item == null)
            {
                p.Value = DBNull.Value;
            }
            else
            {
                if (item.GetType() == typeof(Guid))
                {
                    p.Value = item.ToString();
                    p.DbType = DbType.String;
                    p.Size = 4000;
                }
                else if (item.GetType() == typeof(ExpandoObject))
                {
                    var d = (IDictionary<string, object>)item;
                    p.Value = d.Values.FirstOrDefault();
                }
                else
                {
                    p.Value = item;
                }

                // from DataChomp
                if (item.GetType() == typeof(string))
                {
                    p.Size = 4000;
                }
            }

            cmd.Parameters.Add(p);
        }

        /// <summary>
        /// Turns an IDataReader to a Dynamic list of things
        /// </summary>
        /// <param name="rdr">The reader.</param>
        /// <returns>A list of dynamic things.</returns>
        /// <remarks></remarks>
        public static List<dynamic> ToExpandoList(this IDataReader rdr)
        {
            var result = new List<dynamic>();
            while (rdr.Read())
            {
                dynamic e = new ExpandoObject();
                var d = (IDictionary<string, object>)e;
                for (var i = 0; i < rdr.FieldCount; i++)
                {
                    d.Add(rdr.GetName(i), rdr[i]);
                }

                result.Add(e);
            }

            return result;
        }

        /// <summary>
        /// Turns the object into an ExpandoObject
        /// </summary>
        /// <param name="o">The o.</param>
        /// <returns>An ExpandoObject.</returns>
        /// <remarks></remarks>
        public static dynamic ToExpando(this object o)
        {
            var result = new ExpandoObject();
            var d = result as IDictionary<string, object>; // work with the Expando as a Dictionary
            if (o.GetType() == typeof(ExpandoObject))
            {
                return o; // shouldn't have to... but just in case
            }

            if (o.GetType() == typeof(NameValueCollection))
            {
                var nv = (NameValueCollection)o;
                nv.Cast<string>().Select(key => new KeyValuePair<string, object>(key, nv[key])).ToList().ForEach(d.Add);
            }
            else
            {
                var props = o.GetType().GetProperties();
                foreach (var item in props)
                {
                    d.Add(item.Name, item.GetValue(o, null));
                }
            }

            return result;
        }

        /// <summary>
        /// Turns the object into a Dictionary
        /// </summary>
        /// <param name="thingy">The thingy.</param>
        /// <returns>A Dictionary{string, object}.</returns>
        /// <remarks></remarks>
        public static IDictionary<string, object> ToDictionary(this object thingy)
        {
            return (IDictionary<string, object>)thingy.ToExpando();
        }
    }
}