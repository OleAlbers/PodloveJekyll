﻿

using Feed2Md.Feed;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Feed2Md
{
    public enum CleanMode
    {
        InTextFile,
        InPath,
        InFile
    }

    public static class TokenHelper
    {

        public static string ReplaceTokens(string textContainingTokens, object rootObject, string prefix)
        {
            textContainingTokens=ReplaceLoops(textContainingTokens, rootObject, prefix);
            var allTokens = GetTokensinString(textContainingTokens);
            foreach (var token in allTokens)
            {
                var replacedTokenProperty = GetTokenReplacement(prefix, rootObject, token, out object replacedToken);
                if (replacedTokenProperty != null) textContainingTokens = textContainingTokens.Replace($"[{token}]", replacedToken?.ToString());
            }
            return textContainingTokens;
        }

        public static string CleanString(this string pathContainingTokens, CleanMode cleanMode = CleanMode.InTextFile)
        {
            var cleanedString = pathContainingTokens.Replace("\\[", "[").Replace("\\]", "]");
            
            switch (cleanMode)
            {
             
                
                case CleanMode.InFile:
                    // TODO: MaxLenth
                    cleanedString= Regex.Replace(NoUtf(cleanedString), @"[^\w\d\.]", "_");


                    break;
                case CleanMode.InPath:
                    cleanedString= Regex.Replace(cleanedString, @"[^\w\d\\\/]", "");
                    break;
          
            }

            return cleanedString;
        }


        private static string NoUtf(string content)
        {
            while (true)
            {
                int utfstart = content.IndexOf("&#");
                if (utfstart < 0) break;
                int utfend = content.IndexOf(";", utfstart);
                if (utfend < utfstart) break;
                content = content.Replace(content.Substring(utfstart, utfend - utfstart+1),"_");
            }
            return content;
        }

        private static List<string> GetTokensinString(string pathContainingTokens)
        {
            var foundTokens = new List<string>();
            bool inToken = false;
            string currentToken = null;
            bool ignorenext = false;
            for (int i = 0; i < pathContainingTokens.Length; i++)
            {
                if (ignorenext)
                {
                    ignorenext = false;
                    continue;
                }
                if (pathContainingTokens[i] == '\\')
                {
                    ignorenext = true;
                    continue;
                }
                if (!inToken)
                {
                    if (pathContainingTokens[i] == '[')
                    {
                        inToken = true;
                        currentToken = string.Empty;
                    }
                }
                else
                {
                    if (pathContainingTokens[i] == ']')
                    {
                        inToken = false;
                        foundTokens.Add(currentToken);
                    }
                    else
                    {
                        currentToken += pathContainingTokens[i];
                    }
                }
            }
            return foundTokens;
        }

  

        private static string ReplaceLoops(string textContainingTokens, object rootObject, string prefix)
        {
            bool hasLoops = true;
            while (true)
            {
                int loopStart = textContainingTokens.IndexOf("[LOOP ");
                if (loopStart < 0)
                    break;
                int eol = textContainingTokens.IndexOf("]", loopStart);
                int loopEnd = textContainingTokens.IndexOf("[POOL]");
                if (loopEnd < loopStart || eol < loopStart)
                    break;
                string innnerText = textContainingTokens.Substring(eol + 1, loopEnd - eol-1);
                string outerText= textContainingTokens.Substring(loopStart, loopEnd - loopStart+6);

                string listPropertyName = textContainingTokens.Substring(loopStart + 6, eol - loopStart - 6);
                var property = GetTokenReplacement(prefix, rootObject, listPropertyName, out object listContent);
                string replacement = string.Empty;
                if (property!=null && listContent!=null)
                {
                    var listProperty = (IEnumerable)listContent;
                    foreach (var item in listProperty)
                    {
                        string innerPrefix = $"{prefix}{property.Name}.{item.GetType().Name}.";
                        string itemContent = ReplaceTokens(innnerText, item, innerPrefix);
                        replacement += itemContent;
                    }
                }
                textContainingTokens= textContainingTokens.Replace(outerText, replacement);

            }
            return textContainingTokens;
        }


    


        private static PropertyInfo GetTokenReplacement(string prefix, object parentObject, string token, out object propertyContent)
        {
            foreach (var property in parentObject.GetType().GetProperties())
            {
                if (!property.CanRead) continue;

                propertyContent = null;
                if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    propertyContent = property.GetValue(parentObject);

                }
                else if (property.Name != "Item") 
                {
                    propertyContent = property.GetValue(parentObject);
                } 
                string propertyPath = prefix + property.Name;
                if (propertyPath == token)
                    return property;
                string child = propertyPath + ".";

                if (token.Contains(child) && propertyContent != null)
                {
                    return GetTokenReplacement(child, propertyContent, token, out propertyContent);
                }
            }

            // Nothing found:
            propertyContent = null;
            return null;    
        }

      
    }
}