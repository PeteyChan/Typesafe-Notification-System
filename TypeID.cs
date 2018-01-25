/// <summary>
/// Helper class that gives any class or struct a unique identifier
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assigns unique ID's to every type
/// </summary>
public class TypeID
{
	static Dictionary<Type, int> ids = new Dictionary<Type, int>();
	static Dictionary<int, Type> types = new Dictionary<int, Type>();

	/// <summary>
	/// Returns a unique ID for this type
	/// </summary>
	public static int GetID(Type type)
	{
		int id;
		if (ids.TryGetValue(type, out id))
		{
			return id;
		}
		else
		{
			int newID = ids.Count;
			ids.Add(type, newID);
			types.Add(newID, type);
			return newID;
		}
	}

	/// <summary>
	/// Returns a unique ID for the Generic Type
	/// </summary>
	public static int GetID<T>()
	{
		return GetID(typeof(T));
	}

	public static Type GetType(int ID)
	{
		Type type;
		if (types.TryGetValue(ID, out type))
		{
			return type;
		}
		Debug.LogErrorFormat("A type with ID:{0} has not yet been registered, returning null");
		return null;
	}

	public static int GetID(object obj)
	{
		return GetID(obj.GetType());
	}
}

public class TypeID<T>: TypeID
{
	static bool hasID;
	static int id;
	public static int ID
	{
		get
		{
			if (!hasID)
			{
				id = GetID(typeof(T));
				hasID = true;
			}
			return id;
		}
	}
}
