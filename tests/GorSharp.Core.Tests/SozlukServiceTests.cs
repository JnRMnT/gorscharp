using GorSharp.Core.Sozluk;
using Xunit;

namespace GorSharp.Tests.Core;

public class SozlukServiceTests
{
    private const string MinimalSozlukJson = """
    {
      "version": "0.1.0-test",
      "keywords": {
        "yazdır": { "csharp": "Console.Write", "category": "io" }
      },
      "types": {
        "sayı": { "csharp": "int" },
        "metin": { "csharp": "string" }
      },
      "literals": {
        "doğru": { "csharp": "true", "type": "bool" }
      },
      "operators": {
        "ve": { "csharp": "&&", "category": "logical" }
      },
      "accessModifiers": {
        "genel": { "csharp": "public" }
      },
      "suffixes": {
        "dative": {
          "name": "Yönelme Hali",
          "markers": ["-e", "-a", "-ye", "-ya"],
          "verbs": { "ekle": "Add" }
        }
      }
    }
    """;

    [Fact]
    public void LoadFromJson_ParsesAllSections()
    {
        var service = new SozlukService();
        service.LoadFromJson(MinimalSozlukJson);
        var data = service.Data;

        Assert.Equal("0.1.0-test", data.Version);
        Assert.Single(data.Keywords);
        Assert.Equal(2, data.Types.Count);
        Assert.Single(data.Literals);
        Assert.Single(data.Operators);
        Assert.Single(data.AccessModifiers);
        Assert.Single(data.Suffixes);
    }

    [Fact]
    public void Types_MapCorrectly()
    {
        var service = new SozlukService();
        service.LoadFromJson(MinimalSozlukJson);
        var data = service.Data;

        Assert.True(data.Types.TryGetValue("sayı", out var sayiEntry));
        Assert.Equal("int", sayiEntry!.CSharp);

        Assert.True(data.Types.TryGetValue("metin", out var metinEntry));
        Assert.Equal("string", metinEntry!.CSharp);
    }

    [Fact]
    public void Keywords_MapCorrectly()
    {
        var service = new SozlukService();
        service.LoadFromJson(MinimalSozlukJson);

        Assert.True(service.Data.Keywords.TryGetValue("yazdır", out var entry));
        Assert.Equal("Console.Write", entry!.CSharp);
        Assert.Equal("io", entry.Category);
    }

    [Fact]
    public void Suffixes_ContainVerbs()
    {
        var service = new SozlukService();
        service.LoadFromJson(MinimalSozlukJson);

        Assert.True(service.Data.Suffixes.TryGetValue("dative", out var entry));
        Assert.Equal("Add", entry!.Verbs["ekle"]);
    }

    [Fact]
    public void Suffixes_VerbMappings_AreNormalizedForRuntime()
    {
        const string json = """
        {
          "version": "0.1.0-test",
          "suffixes": {
            "dative": {
              "markers": ["'ye", "'ya"],
              "verbMappings": {
                "ekle": { "csharp": ".Add({arg})", "description": "test" }
              }
            }
          }
        }
        """;

        var service = new SozlukService();
        service.LoadFromJson(json);

        Assert.True(service.Data.Suffixes.TryGetValue("dative", out var entry));
        Assert.True(entry!.TryResolveVerbMethodName("ekle", out var method));
        Assert.Equal("Add", method);
    }

    [Fact]
    public void MissingKey_ReturnsFalse()
    {
        var service = new SozlukService();
        service.LoadFromJson(MinimalSozlukJson);

        Assert.False(service.Data.Types.ContainsKey("bilinmeyen"));
    }

    [Fact]
    public void Data_ThrowsIfNotLoaded()
    {
        var service = new SozlukService();
        Assert.Throws<InvalidOperationException>(() => service.Data);
    }
}
