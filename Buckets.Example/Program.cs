using System.Text.Json;
using Buckets.Example;
using BucketsDB;



Bucket<Songs> _bucket = new Bucket<Songs>("favMusic", 4);
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

Songs controlSong = _bucket.FindByKeyValue("Name", "Rasputin");
Console.WriteLine(JsonSerializer.Serialize(controlSong));
