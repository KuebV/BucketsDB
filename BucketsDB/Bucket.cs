using System.Collections;
using System.ComponentModel;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using BucketsDB.Property;

namespace BucketsDB;

public class Bucket<T> where T : new()
{
    
    /// <summary>
    /// Name of the bucket
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Maximum amount of items that can be placed into the specified bucket
    /// </summary>
    public int MaxItems { get; }

    /// <summary>
    /// If you try to add a different data type from the already existing types, then it will throw an exception
    /// </summary>
    private bool _ignoreTypeCheck { get; }
    
    /// <summary>
    /// The specified path in which the bucketName.bd is located
    /// </summary>
    private string _bucketPropertiesPath { get; }
    
    /// <summary>
    /// If the current bucket is filled, then a new bucket is created to handle the new items
    /// </summary>
    private bool _manageOverflow { get; }

    private BucketProperty _properties { get; set; }

    private string _bucketPath { get; set; }

    private bool beenInitalized;

    private Stream _stream;
    
    public int Count { get; private set; }

    public Bucket(string _name, int maxItems, string configPath = "", bool ignoreType = false,
        bool autoManageOverflow = false)
    {
        Name = _name;
        MaxItems = maxItems;

        _bucketPropertiesPath = string.IsNullOrEmpty(configPath) ? Path.Combine(Directory.GetCurrentDirectory(), $"{_name}.bp") : configPath;
        _ignoreTypeCheck = ignoreType;
        
        _manageOverflow = autoManageOverflow;
        beenInitalized = false;
    }

    public void Init()
    {
        if (!File.Exists(_bucketPropertiesPath))
        {
            new Properties(_bucketPropertiesPath).Write(new BucketProperty
            {
                MaxItems = MaxItems,
                CreationDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
                IgnoreTypeCheck = _ignoreTypeCheck,
                CurrentOverflowBucket = $"{Name}.bucket",
                HomeBucket = $"{Name}.bucket",
                CurrentlyOverflowing = false,
                ManageOverflow = _manageOverflow
            });
        }
        
        _properties = new Properties(_bucketPropertiesPath).Read<BucketProperty>();
        if (_properties.CurrentlyOverflowing)
            _bucketPath = _properties.CurrentOverflowBucket!;
        else
            _bucketPath = $"{Name}.bucket";
        
        if (!File.Exists(_bucketPath))
            using (StreamWriter sw = new StreamWriter(_bucketPath))
                sw.Close();

        _stream = File.Open(_bucketPath, FileMode.Append);

        beenInitalized = true;
    }

    /// <summary>
    /// Append a value to the bucket
    /// </summary>
    /// <param name="obj"></param>
    /// <typeparam name="TValue"></typeparam>
    public void Add<TValue>(TValue obj)
    {
        if (!beenInitalized)
            throw new Exception("Bucket has not been initialized");

        PropertyInfo[] propertyInfo = obj.GetType().GetProperties();
        int properties = propertyInfo.Length;
        
        using (BinaryWriter bin = new BinaryWriter(_stream, Encoding.UTF8, true))
        {
            bin.Write((byte)properties); // Cast to 1 byte

            for (int i = 0; i < properties; i++)
            {
                if (propertyInfo[i].PropertyType is { IsPrimitive: false, Name: "System.String" })
                    throw new InvalidDataException($"{propertyInfo[i].Name} is not a primitive!");

                string? value = propertyInfo[i].GetValue(obj).ToString();

                TypeCode typeCode = Type.GetTypeCode(propertyInfo[i].PropertyType);
                switch (typeCode)
                {
                    case TypeCode.Boolean:
                        bin.Write(bool.Parse(value));
                        break;
                    case TypeCode.Byte:
                        bin.Write(byte.Parse(value));
                        break;
                    case TypeCode.Char:
                        bin.Write(char.Parse(value));
                        break;
                    case TypeCode.Decimal:
                        bin.Write(decimal.Parse(value));
                        break;
                    case TypeCode.Double:
                        bin.Write(double.Parse(value));
                        break;
                    case TypeCode.UInt16:
                    case TypeCode.Int16:
                        bin.Write(Int16.Parse(value));
                        break;
                    case TypeCode.UInt32:
                    case TypeCode.Int32:
                        bin.Write(Int32.Parse(value));
                        break;
                    case TypeCode.UInt64:
                    case TypeCode.Int64:
                        bin.Write(Int64.Parse(value));
                        break;
                    case TypeCode.String:
                        bin.Write(value);
                        break;
                }

            }
        }

        Count++;

        if (Count > MaxItems && _properties.ManageOverflow)
        {
            CreateOverflowBucket();
        }
    }

    public List<T> GetItems()
    {
        List<FileInfo> buckets = GetBuckets().ToList();

        List<T> totalCollection = new List<T>();
        foreach (FileInfo bucket in buckets)
        {
            List<T> items = ReadFile<T>(bucket);
            for (int i = 0; i < items.Count; i++)
                totalCollection.Add(items[i]);
        }
        
        return totalCollection;
    }

    private List<T> ReadFile<T>(FileInfo bucket) where T : new()
    {
        List<T> objList = new List<T>();

        _stream = File.Open(bucket.FullName, FileMode.Open);
        using (BinaryReader reader = new BinaryReader(_stream))
        {
            while (reader.PeekChar() != 0x01)
            {
                T obj = new T();
                PropertyInfo[] objProps = obj.GetType().GetProperties();

                int propLength = 0;
                try
                {
                    propLength = reader.ReadByte();
                }
                catch (EndOfStreamException ex)
                {
                    break;
                }
                if (propLength != typeof(T).GetProperties().Length)
                    throw new InvalidDataException($"Property Mismatch!\n{obj.GetType().Name}({propLength}) - {obj.GetType().Name} ({typeof(T).GetProperties().Length})");

                for (int i = 0; i < propLength; i++)
                {
                    TypeCode typeCode = Type.GetTypeCode(objProps[i].PropertyType);
                    object? value = null;
                    switch (typeCode)
                    {
                        case TypeCode.Boolean:
                            value = reader.ReadBoolean();
                            break;
                        case TypeCode.Byte:
                            value = reader.ReadByte();
                            break;
                        case TypeCode.Char:
                            value = reader.ReadChar();
                            break;
                        case TypeCode.Decimal:
                            value = reader.ReadDecimal();
                            break;
                        case TypeCode.Double:
                            value = reader.ReadDouble();
                            break;
                        case TypeCode.UInt16:
                        case TypeCode.Int16:
                            value = reader.ReadInt16();
                            break;
                        case TypeCode.UInt32:
                        case TypeCode.Int32:
                            value = reader.ReadInt32();
                            break;
                        case TypeCode.UInt64:
                        case TypeCode.Int64:
                            value = reader.ReadInt64();
                            break;
                        case TypeCode.String:
                            value = reader.ReadString();
                            break;
                        default:
                            break;
                    }

                    objProps[i].SetValue(obj, value);
                }

                objList.Add(obj);
            }
        }
        

        return objList;
        
    }

    public T FindByKeyValue(string key, object? value)
    {
        List<T> items = GetItems();
        for (int i = 0; i < items.Count(); i++)
        {
            PropertyInfo propertyInfo = items[i].GetType().GetProperty(key);
            if (propertyInfo.GetValue(items[i]).Equals(value))
                return items[i];
        }

        return new T();
    }
    
    public List<T> FindMultipleByKeyValue(string key, object? value)
    {
        List<T> items = GetItems();
        List<T> obj = new List<T>();
        for (int i = 0; i < items.Count; i++)
        {
            PropertyInfo propertyInfo = items[i].GetType().GetProperty(key);
            if (propertyInfo.GetValue(items[i]).Equals(value))
                obj.Add(items[i]);
        }

        return obj;

    }

    /// <summary>
    /// Creates an overflow bucket when the current bucket is overflowing
    /// </summary>
    private void CreateOverflowBucket()
    {
        FileInfo oldBucketInfo = new FileInfo(_bucketPath);
        
        string getParentDirectory = oldBucketInfo.Directory.FullName;
        string fileName = oldBucketInfo.Name;
        string fileExt = fileName.Substring(fileName.IndexOf('.'));
        fileName = fileName.Replace(fileExt, "");
        
        if (fileName.Contains("-overflow"))
            fileName = fileName.Substring(0, fileName.IndexOf('-'));

        int bucketCount = 0;
        string bucketOverflowFile = "";
        while (true) // Realistically this shouldn't be an issue, but it's still rather risky
        {
            bucketOverflowFile = Path.Combine(getParentDirectory, $"{fileName}-overflow-{bucketCount}{fileExt}");
            if (!File.Exists(bucketOverflowFile))
                break;
            bucketCount++;
        }

        _bucketPath = bucketOverflowFile;
        _properties.CurrentlyOverflowing = true;
        _properties.CurrentOverflowBucket = new FileInfo(bucketOverflowFile).Name;
        Count = 0;
        
        UpdateBucketProperties();
    }

    private FileInfo[] GetOverflowBuckets()
    {
        FileInfo[] files = new FileInfo(_properties.HomeBucket).Directory.GetFiles();
        return files.Where(x => x.Name.Contains($"{Name}-overflow")).ToArray();
    }

    private FileInfo[] GetBuckets()
    {
        FileInfo[] files = new FileInfo(_bucketPath).Directory.GetFiles();
        List<FileInfo> fileList = files.Where(x => x.Name.Contains($"{Name}-overflow")).ToList();
        fileList.Add(new(_properties.HomeBucket));
        return fileList.ToArray();
    }

    /// <summary>
    /// Overwrites the previous bucket properties file
    /// </summary>
    private void UpdateBucketProperties()
        => new Properties(_bucketPropertiesPath).Write(_properties);


    private object? GetValue(PropertyInfo propertyInfo, string value)
    {
        Type datatype = Type.GetType(propertyInfo.PropertyType.ToString()); // This is janky af
        if (datatype == null)
            return null;

        if (datatype.IsArray)
            return null; // TODO

        TypeConverter converter = TypeDescriptor.GetConverter(datatype);
        object? result = converter.ConvertFrom(value);
        return result;
    }

    private string FormatKeyValuePair(string key, object? value)
        => $"{key}0x00{value}";

    private string GetKeyFromRaw(string rawString)
        => rawString.Substring(0, rawString.IndexOf("0x00", StringComparison.Ordinal));

    private string GetValueFromRaw(string rawString)
        => rawString.Remove(0, GetKeyFromRaw(rawString).Length + 4);

    public void Close()
    {
        _stream.Close();
        _stream = File.Open(_bucketPath, FileMode.Append);
        using (BinaryWriter writer = new BinaryWriter(_stream, Encoding.UTF8, false))
        {
            writer.Write(0x01);
            writer.Close();
        }
        _stream.Close();
    }
}