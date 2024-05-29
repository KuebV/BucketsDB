using System.ComponentModel;
using System.Reflection;

namespace BucketsDB.Property;

public class Properties
{
    private string _path;
    
    public Properties(string path)
    {
        _path = path;
    }

    public void Write<TValue>(TValue obj)
    {
        using (StreamWriter sw = new StreamWriter(_path))
        {
            PropertyInfo[] typeProperties = obj.GetType().GetProperties();
            for (int i = 0; i < typeProperties.Length; i++)
            {
                PropertyInfo propertyInfo = typeProperties[i];
                /*if (!propertyInfo.GetType().IsPrimitive || propertyInfo.GetType() != typeof(string))
                    throw new Exception("Cannot write " + propertyInfo.GetType());
                    */
                
                if (propertyInfo.GetCustomAttributes().Any())
                {
                    string comments = propertyInfo.GetCustomAttribute<PropertyAttribute>().Comments;
                    sw.WriteLine($"# {comments}");
                }
                
                if (propertyInfo.PropertyType.IsArray)
                    sw.WriteLine($"{propertyInfo.Name}: {string.Join(' ', propertyInfo.GetValue(obj))}\n");
                else
                    sw.WriteLine($"{propertyInfo.Name}: {propertyInfo.GetValue(obj)}\n");
            }
            
            sw.Close();
        }
    }

    public T Read<T>() where T : new()
    {
        string[] fileLines = File.ReadAllLines(_path);

        T obj = new T();

        for (int i = 0; i < fileLines.Length; i++)
        {
            if (string.IsNullOrEmpty(fileLines[i]) || fileLines[i].StartsWith("#"))
                continue;

            string key = fileLines[i].Split(':')[0];
            string value = fileLines[i].Split(':')[1].Remove(0, 1);

            PropertyInfo correspondingProperty = typeof(T).GetProperty(key);

            Type datatype = Type.GetType(correspondingProperty.PropertyType.ToString()); // This is janky af
            if (datatype == null)
                continue;
            
            TypeConverter converter = TypeDescriptor.GetConverter(datatype);
            object? result = converter.ConvertFrom(value);
            correspondingProperty.SetValue(obj, result);
        }

        return obj;
    }

}