namespace ProcurementSystem.Infrastructure.Matching;

/// <summary>
/// Разреженная TF-IDF матрица наименований каталога. Косинус в этом пространстве —
/// базовый признак «похожести» для логистической модели (ТЗ п.5.3: TF-IDF как допустимый выбор).
/// </summary>
internal sealed class TfIdfIndex
{
    private readonly Dictionary<string, int> _df = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Dictionary<string, double>> _vectors = [];
    private readonly Dictionary<string, List<Guid>> _postings = new(StringComparer.Ordinal);
    private int _n;

    public int DocumentCount => _n;

    public void Build(IEnumerable<(Guid Id, string Name)> docs)
    {
        _df.Clear();
        _vectors.Clear();
        _postings.Clear();
        var bags = new List<(Guid Id, List<string> Tokens)>();
        foreach (var (id, name) in docs)
        {
            var tokens = TextFeatures.Tokens(name).ToList();
            if (tokens.Count == 0) continue;
            bags.Add((id, tokens));
            foreach (var t in tokens.Distinct())
                _df[t] = _df.GetValueOrDefault(t) + 1;
        }
        _n = Math.Max(bags.Count, 1);
        foreach (var (id, tokens) in bags)
        {
            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var t in tokens) tf[t] = tf.GetValueOrDefault(t) + 1;
            var vec = new Dictionary<string, double>(StringComparer.Ordinal);
            double normSq = 0;
            foreach (var (t, c) in tf)
            {
                var idf = Math.Log((_n + 1.0) / (_df.GetValueOrDefault(t) + 1.0)) + 1.0;
                var w = (1.0 + Math.Log(c)) * idf;
                vec[t] = w;
                normSq += w * w;
                if (!_postings.TryGetValue(t, out var list))
                    _postings[t] = list = [];
                list.Add(id);
            }
            var norm = Math.Sqrt(normSq);
            if (norm > 0)
            {
                foreach (var k in vec.Keys.ToList()) vec[k] /= norm;
            }
            _vectors[id] = vec;
        }
    }

    public Dictionary<string, double> Vectorize(string name)
    {
        var tokens = TextFeatures.Tokens(name);
        var tf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in tokens) tf[t] = tf.GetValueOrDefault(t) + 1;
        var vec = new Dictionary<string, double>(StringComparer.Ordinal);
        double normSq = 0;
        foreach (var (t, c) in tf)
        {
            if (!_df.ContainsKey(t)) continue;
            var idf = Math.Log((_n + 1.0) / (_df[t] + 1.0)) + 1.0;
            var w = (1.0 + Math.Log(c)) * idf;
            vec[t] = w;
            normSq += w * w;
        }
        var norm = Math.Sqrt(normSq);
        if (norm > 0)
            foreach (var k in vec.Keys.ToList()) vec[k] /= norm;
        return vec;
    }

    public double Cosine(Guid productId, Dictionary<string, double> query)
    {
        if (!_vectors.TryGetValue(productId, out var doc) || query.Count == 0) return 0;
        double sum = 0;
        foreach (var (t, w) in query)
            if (doc.TryGetValue(t, out var dw)) sum += w * dw;
        return sum;
    }

    public IEnumerable<Guid> Candidates(string name, int limit)
    {
        var tokens = TextFeatures.Tokens(name)
            .Where(t => _postings.ContainsKey(t))
            .OrderBy(t => _postings[t].Count)
            .Take(12);
        var seen = new HashSet<Guid>();
        foreach (var t in tokens)
        {
            foreach (var id in _postings[t])
            {
                if (seen.Add(id) && seen.Count >= limit) return seen;
            }
        }
        return seen;
    }
}
