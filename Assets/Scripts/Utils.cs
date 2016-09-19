// This class contains some helper functions.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Networking;

public class Utils {
    // Mathf.Max only works for float and int, not long
    public static long MaxLong(long a, long b) {
        return a > b ? a : b;
    }

    // Mathf.Min only works for float and int, not long
    public static long MinLong(long a, long b) {
        return a < b ? a : b;
    }

    // Mathf.Clamp only works for float and int, not long
    public static long ClampLong(long value, long min, long max) {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    
    // generate a random vector on the x-z plane (y=0)
    public static Vector3 RandVec3XZ() {
        // note: '.f' is important so that Random.Range knows we want floats
        return new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
    }

    // is any of the keys UP?
    public static bool AnyKeyUp(KeyCode[] keys) {
        foreach (KeyCode k in keys)
            if (Input.GetKeyUp(k)) return true;
        return false;
    }

    // is any of the keys DOWN?
    public static bool AnyKeyDown(KeyCode[] keys) {
        foreach (KeyCode k in keys)
            if (Input.GetKeyDown(k)) return true;
        return false;
    }

    // detect headless mode (which has graphicsDeviceType Null)
    public static bool IsHeadless() {
        return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
    }

    // String.IsNullOrWhiteSpace that exists in NET4.5
    // note: can't be an extension because then it can't detect null strings
    //       like null.IsNullorWhitespace
    public static bool IsNullOrWhiteSpace(string value) {
        return System.String.IsNullOrEmpty(value) || value.Trim().Length == 0;
    }
}
