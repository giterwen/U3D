using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class Json2TableTools
{
	/// <summary>
	/// json表转换成lua表
	/// </summary>
	/// <param name="json"></param>
	/// <returns></returns>
	public static string GetLuaFromJson(string json)
	{
		using (MemoryStream stream = new MemoryStream())
		using (StreamWriter writer = new StreamWriter(stream))
		{
			JObject jObject = JObject.Parse(json);
			writer.Write("local config = ");
			Json2Table(jObject.ToString(), writer);
			writer.Write("return config");
			writer.Flush();
			writer.Close();
			var bs = stream.ToArray();
			return Encoding.UTF8.GetString(bs);
		}
	}
	/// <summary>
	/// lua表装换成json表
	/// </summary>
	/// <param name="lua"></param>
	/// <returns></returns>
	public static string GetJsonFromLua(string lua)
	{
		using (MemoryStream stream = new MemoryStream())
		using (StreamWriter writer = new StreamWriter(stream))
		{
			Table2Json(lua, writer);
			writer.Flush();
			writer.Close();
			var bs = stream.ToArray();
			return Encoding.UTF8.GetString(bs);
		}
	}
	/// <summary>
	/// 初始化配置列表
	/// </summary>
	public static void OnInitConfigList(string path)
	{
		var files = Directory.GetFiles(path).Where((p) => p.EndsWith(".lua")).Select((p) => Path.GetFileName(p).Replace(".lua", string.Empty)).ToList();

		using (FileStream file = new FileStream(path + @"\ConfigSetting.lua", FileMode.Create))
		using (StreamWriter writer = new StreamWriter(file))
		{
			writer.WriteLine(@"local config = {");
			for (int i = 0; i < files.Count; i++)
			{
				if (i == files.Count - 1)
				{
					writer.WriteLine(string.Format(@"  [""{0}""] = 1", files[i]));
				}
				else
				{
					writer.WriteLine(string.Format(@"  [""{0}""] = 1,", files[i]));
				}
			}
			writer.WriteLine(@"}");
			writer.WriteLine(@"return config");
		}

	}

	static void Json2Table(string file, StreamWriter writer)
	{

		byte[] array = Encoding.UTF8.GetBytes(file);
		using (MemoryStream stream = new MemoryStream(array))
		using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
		{
			while (!reader.EndOfStream)
			{
				var context = reader.ReadLine();


				if (context.EndsWith("[],"))
				{
					context = context.Substring(0, context.Length - 3) + "{},";
				}
				else if (context.EndsWith("[]"))
				{
					context = context.Substring(0, context.Length - 2) + "{}";


					//context = context.Replace("]", "}");
				}
				else if (context.EndsWith("["))
				{
					context = context.Substring(0, context.Length - 1) + "{";
					//context = context.Replace("],", "},");
				}
				else if (context.EndsWith("]"))
				{
					context = context.Substring(0, context.Length - 1) + "}";
					//context = context.Replace("],", "},");
				}
				else if (context.EndsWith("],"))
				{
					context = context.Substring(0, context.Length - 2) + "},";
					//context = context.Replace("],", "},");
				}

				if (context.Contains("null"))
				{
					Console.WriteLine("null Value=> " + context);
					context = context.Replace("null", "nil");
				}

				if (context.Contains(":"))
				{
					var test = context.Replace(" ", string.Empty);
					var index = test.IndexOf(':');
					if (test.ToArray()[index - 1] == '"')
					{
						///满足条件
						index = context.IndexOf(':');
						var key = context.Substring(0, index);
						var count = GetSpaceCout(key);
						key = key.Trim().Replace(@"""", string.Empty);
						var value = context.Substring(index + 1, context.Length - index - 1).Trim();
						long number = 0;
						if (long.TryParse(key, out number))
						{
							key = string.Format(@"[""{0}""]", key);
						}
						writer.WriteLine(GetSpace(count) + key + " = " + value);
					}
					else
					{
						writer.WriteLine(context);
					}

				}
				else
				{
					writer.WriteLine(context);
				}

			}
		}
	}
	static void Table2Json(string file, StreamWriter writer)
	{
		List<int> cache = new List<int>();
		byte[] array = Encoding.UTF8.GetBytes(file);
		using (MemoryStream stream = new MemoryStream(array))
		using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
		{
			List<string> source = new List<string>();
			while (!reader.EndOfStream)
			{
				source.Add(reader.ReadLine());


			}

			for (int i = 0; i < source.Count; i++)
			{
				var context = source[i];
				if (context.Contains("local config = "))
				{
					context = context.Replace("local config = ", string.Empty);
				}
				if (context.Contains("return config"))
				{
					context = context.Replace("return config", string.Empty);
				}
				if (context.Contains("nil"))
				{
					Console.WriteLine("nil Value=> " + context);
					context = context.Replace("nil", "null");
				}

				if (context.Contains("="))
				{
					var index = context.IndexOf('=');
					if (context.ToArray()[index - 1] == ' ' && context.ToArray()[index + 1] == ' ')
					{
						///满足条件
						index = context.IndexOf('=');
						var key = context.Substring(0, index);
						var count = GetSpaceCout(key);
						key = key.Trim();
						var value = context.Substring(index + 1, context.Length - index - 1).Trim();
						if (key.Contains("[") || key.Contains("]"))
						{
							key = key.Replace("[", string.Empty).Replace("]", string.Empty);
						}
						if (!key.Contains(@""""))
						{
							key = string.Format(@"""{0}""", key);
						}

						if (value == "{}")
						{
							value = "[]";
						}
						if (value == "{},")
						{
							value = "[],";
						}

						if (value == "{")
						{
							string nextLine = source[i + 1].Trim();
							if (nextLine.StartsWith("{"))
							{
								value = "[";
								cache.Add(count);
							}
							else if (!nextLine.Contains(" = "))
							{
								value = "[";
								cache.Add(count);
							}
						}

						writer.WriteLine(GetSpace(count) + key + " : " + value);
					}
					else
					{
						if (!string.IsNullOrEmpty(context))
						{
							writer.WriteLine(context);
						}
					}
				}
				else
				{
					var test = context.Trim();
					if (test.StartsWith("}"))
					{
						var count = GetSpaceCout(context);
						if (cache.Count > 0 && cache[cache.Count - 1] == count)
						{
							context = context.Replace("}", "]");
							cache.RemoveAt(cache.Count - 1);
						}
					}


					if (!string.IsNullOrEmpty(context))
					{
						writer.WriteLine(context);
					}
				}
			}
		}
	}
	static string GetSpace(int count)
	{
		string str = string.Empty;
		for (int i = 0; i < count; i++)
		{
			str += " ";
		}
		return str;
	}
	static int GetSpaceCout(string str)
	{
		int count = 0;
		var array = str.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == ' ')
			{
				count++;
			}
			else
			{
				break;
			}
		}
		return count;
	}
}