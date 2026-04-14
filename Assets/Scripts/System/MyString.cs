using System;
using System.Collections.Generic;
using UnityEngine;

public static class MyString 
{
    private static Dictionary<string, string>  colors;
   public static string ColorText(string text, string colorId)
    {
        ColorInit();
        try
        {
            var result = "<color=#" + colors[colorId] + ">" + text + "</color>";
            return result;
        }
        catch
        {
            Debug.LogError("color " + colorId + " in text " + text + "no found");
            return "error";
        }
        
    }

    private static void ColorInit()
    {
        if (colors == null)
        {
            var col = Resources.LoadAll<ColorCode>("Colors");
            colors = new Dictionary<string, string>();
            for (int i = 0; i < col.Length; i++)
            {
                colors.Add(col[i].id, col[i].colorCode);
            }
        }
    }
    public static Color ColorForID(string id)
    {
        ColorInit();
        Color c = new Color();
        ColorUtility.TryParseHtmlString("#"+colors[id], out c);
        return c;
    }
    public static string SetColorText(string res)
    {
        while (true)
        {
            int sIdx = res.IndexOf("color_");
            if (sIdx == -1)
            {
                return res;
            }
            int eIdx = res.IndexOf("_", sIdx+1);
            int lg = res.IndexOf(" ", eIdx) - eIdx-1;
            string colIdx = res.Substring(sIdx + 6, lg);
            int sWord = sIdx+6 + colIdx.Length+1;
            int lWord = res.IndexOf("&", sWord) - sWord;
            string word = res.Substring(sWord,lWord);
            res = res.Remove(sIdx, sWord+lWord-sIdx+1);
            res = res.Insert(sIdx, ColorText(word, colIdx));
        }
    }
    public static string SetValueInText(string[] value, string res, string colorID)
    {
        if (value == null) return res;

        while (true)
        {
            int sIdx = res.IndexOf("value_");
            if (sIdx == -1)
            {
                return res;
            }
            int eIdx = res.IndexOf("_", sIdx - 1);
            string v = res.Substring(eIdx + 1, 1);
            res = res.Replace("value_" + v, colorID != null ? ColorText(value[int.Parse(v)], colorID) : value[int.Parse(v)]);
        }
    }
    public static string SetValueInText(string[] value, string res)
    {
        return SetValueInText(value, res, null);
    }
    public static string GetDH(double data)
    {
        TimeSpan ts = TimeSpan.FromSeconds(data);
        return string.Format("{0:D2}d{1:D2}h", ts.Days, ts.Hours);
    }
    public static string GetHM(double data)
    {
        TimeSpan ts = TimeSpan.FromSeconds(data);
        return string.Format("{0:D2}:{1:D2}", ts.Hours, ts.Minutes);
    }
    public static string GetHMS(double data)
    {
        TimeSpan ts = TimeSpan.FromSeconds(data);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", ts.Hours, ts.Minutes, ts.Seconds);
    }
    public static string GetMS(double data)
    {
        TimeSpan ts = TimeSpan.FromSeconds(data);
        return string.Format("{0:D2}:{1:D2}", ts.Minutes, ts.Seconds);
    }
    public static string GetAutoTime(double data)
    {
        if (data < 3600)
        {
            return GetMS(data);
        }
        if (data < 86400)
        {
            return GetHMS(data);
        }
        return GetDH(data);
    }
}
