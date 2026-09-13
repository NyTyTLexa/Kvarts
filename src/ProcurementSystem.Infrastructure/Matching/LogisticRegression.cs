namespace ProcurementSystem.Infrastructure.Matching;

/// <summary>
/// Бинарная логистическая регрессия, обучаемая SGD с L2.
/// Признаки нормализованы в [0..1]; выход — P(это тот товар).
/// </summary>
internal sealed class LogisticRegression
{
    public const int Dim = 6;

    public double[] Weights { get; }
    public double Bias { get; private set; }

    /// <summary>Информативный prior: точный артикул и косинус имени сильнее категории.</summary>
    public LogisticRegression()
    {
        Weights = [3.2, 1.8, 2.6, 1.4, 0.9, 0.5];
        Bias = -2.2;
    }

    public double Predict(double[] x)
    {
        var z = Bias;
        for (var i = 0; i < Dim; i++) z += Weights[i] * x[i];
        if (z > 20) return 1;
        if (z < -20) return 0;
        return 1.0 / (1.0 + Math.Exp(-z));
    }

    public void Train(IReadOnlyList<(double[] X, double Y)> samples, int epochs = 25, double lr = 0.12, double l2 = 0.002)
    {
        if (samples.Count == 0) return;
        var rng = new Random(7);
        var order = Enumerable.Range(0, samples.Count).ToArray();
        for (var epoch = 0; epoch < epochs; epoch++)
        {
            rng.Shuffle(order);
            var eta = lr / (1.0 + 0.04 * epoch);
            foreach (var idx in order)
            {
                var (x, y) = samples[idx];
                var p = Predict(x);
                var err = p - y;
                Bias -= eta * err;
                for (var i = 0; i < Dim; i++)
                    Weights[i] -= eta * (err * x[i] + l2 * Weights[i]);
            }
        }
    }
}
