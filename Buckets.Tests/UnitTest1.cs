using System.Text.Json;

namespace Buckets.Tests;

public class Songs
{
    public string Name { get; set; }
    public string Artist { get; set; }
    public int Rating { get; set; }
    public long LastListened { get; set; }
}

public class Tests
{
    private Bucket<Songs> _bucket;
    
    [SetUp]
    public void Setup()
    {
        _bucket = new Bucket<Songs>("favMusic", 4);
        _bucket.Init();
        
        _bucket.Add(new Songs
        {
            Name = "Cherry",
            Artist = "Ratatat",
            LastListened = 0,
            Rating = 9
        });
        
        _bucket.Add(new Songs
        {
            Name = "Control",
            Artist = "Aaron Taos",
            LastListened = 0,
            Rating = 10
        });
        
        _bucket.Add(new Songs
        {
            Name = "Rasputin",
            Artist = "Boney M.",
            LastListened = 0,
            Rating = 8
        });
        
        _bucket.Add(new Songs
        {
            Name = "Troubleman",
            Artist = "Electric Guest",
            LastListened = 0,
            Rating = 7
        });
    }

    [Test]
    public void Test1()
    {
        List<Songs> songsList = _bucket.GetItems();
        
    }
}