using System.IO;
using System.Linq;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Vector Database for RAG semantic search.
/// </summary>
public class VectorDatabase
{
    private readonly string _dbPath;
    private readonly Dictionary<string, float[]> _vectors = new();
    private readonly Dictionary<string, string> _documents = new();

    public VectorDatabase(string dbPath = "")
    {
        _dbPath = string.IsNullOrEmpty(dbPath)
            ? Path.Combine(FileSystem.AppDataDirectory, "vectordb.json")
            : dbPath;
        Load();
    }

    public void Add(string id, float[] embedding, string document)
    {
        _vectors[id] = embedding;
        _documents[id] = document;
    }

    public List<(string Id, string Document, float Score)> Search(float[] query, int topK = 5)
    {
        var results = new List<(string, string, float)>();
        foreach (var (id, emb) in _vectors)
        {
            float score = CosineSimilarity(query, emb);
            results.Add((id, _documents[id], score));
        }
        return results.OrderByDescending(r => r.Item3).Take(topK).ToList();
    }

    public int Count => _vectors.Count;

    public void Save()
    {
        var data = JsonSerializer.Serialize(new { vectors = _vectors, documents = _documents });
        File.WriteAllText(_dbPath, data);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_dbPath)) return;
            var doc = JsonDocument.Parse(File.ReadAllText(_dbPath));
            if (doc.RootElement.TryGetProperty("vectors", out var v))
                foreach (var p in v.EnumerateObject())
                    _vectors[p.Name] = p.Value.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
            if (doc.RootElement.TryGetProperty("documents", out var d))
                foreach (var p in d.EnumerateObject())
                    _documents[p.Name] = p.Value.GetString() ?? "";
        }
        catch { }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, nA = 0, nB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            nA += a[i] * a[i];
            nB += b[i] * b[i];
        }
        return dot / (float)(Math.Sqrt(nA) * Math.Sqrt(nB) + 1e-8);
    }
}
