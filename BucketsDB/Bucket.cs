using System.Collections;
using System.ComponentModel;
using System.Reflection;
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

        using (StreamWriter sw = new StreamWriter(_bucketPath, append: true))
        {
            PropertyInfo[] propertyInfo = obj.GetType().GetProperties();
            for (int i = 0; i < propertyInfo.Length; i++)
            {
                string line = "";
                if (propertyInfo[i].PropertyType.IsArray)
                {
                    object? value = propertyInfo[i].GetValue(obj);
                    if (value is IEnumerable enumerable)
                        line = $"{propertyInfo[i].Name}0x00{string.Join("0xA1", enumerable.Cast<object>())}";
                }
                else
                    line = $"{propertyInfo[i].Name}0x00{propertyInfo[i].GetValue(obj)}";

                sw.WriteLine(line);
            }
            sw.WriteLine("@");
            sw.Close();
        }

        Count++;

        if (Count >= MaxItems)
        {
            CreateOverflowBucket();
        }
    }

    public List<T> GetItems()
    {
        List<FileInfo> buckets = new List<FileInfo> { new(_properties.HomeBucket) };
        if (_properties.CurrentlyOverflowing)
            foreach (FileInfo overflowBucket in GetOverflowBuckets())
                buckets.Add(overflowBucket);

        List<T> totalCollection = new List<T>();
        foreach (FileInfo bucket in buckets)
        {
            List<T> items = ReadFile<T>(bucket);
            for (int i = 0; i < items.Count; i++)
                totalCollection.Add(items[i]);
        }

        Console.WriteLine(totalCollection.Count);
        return totalCollection;
    }

    private List<T> ReadFile<T>(FileInfo bucket) where T : new()
    {
        string[] fileLines = File.ReadAllLines(bucket.Name);
        int propertySize = typeof(T).GetProperties().Length;
        int itemCount = fileLines.Length / propertySize;
        int currentIndex = 0;
        
        List<T> objs = new List<T>();
        for (int i = 0; i < itemCount; i++)
            objs.Add(new T());

        for (int i = 0; i < fileLines.Length; i++)
        {
            if (string.IsNullOrEmpty(fileLines[i]))
                continue;

            if (fileLines[i].StartsWith("@"))
            {
                currentIndex++;
                continue;
            }

            int seperatorIndex = fileLines[i].IndexOf("0x00");
            string key = fileLines[i].Substring(0, seperatorIndex);
            string value = fileLines[i].Remove(0, key.Length + 4);
            PropertyInfo correspondingProperty = typeof(T).GetProperty(key);

            object? result = GetValue(correspondingProperty, value);
            correspondingProperty.SetValue(objs[currentIndex], result);
        }

        return objs;
        
        
    }

    public T FindByKeyValue(string key, object? value)
    {
        FileInfo[] buckets = GetBuckets();
        string matchString = $"{key}0x00{value}";

        T obj = new T();
        
        foreach (FileInfo bucket in buckets)
        {
            string[] fileLines = File.ReadAllLines(bucket.FullName);
            if (!fileLines.Contains(matchString)) // If the bucket doesn't contain KeyValuePair, we skip it.
                continue;

            int indexOf = Array.IndexOf(fileLines, matchString);
            int startOf = 0;

            while (true)
            {
                indexOf--;
                string line = fileLines[indexOf];
                if (line.StartsWith("@"))
                    break;
            }

            for (int i = indexOf + 1; i < (indexOf + typeof(T).GetProperties().Length); i++)
            {
                string foundKey = GetKeyFromRaw(fileLines[i]);
                string foundValue = GetValueFromRaw(fileLines[i]);
                
                PropertyInfo correspondingProperty = typeof(T).GetProperty(foundKey);
                object? result = GetValue(correspondingProperty, foundValue);
                correspondingProperty.SetValue(obj, result);
            }
            
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
        while (true)
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
}