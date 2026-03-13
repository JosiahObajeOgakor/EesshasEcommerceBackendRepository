using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface IIntentClassifier
    {
        /// <summary>
        /// Classify the given message into a label (e.g. "agentic" or "chat") and a confidence between 0 and 1.
        /// </summary>
        Task<(string Label, double Confidence)> ClassifyAsync(string message);
    }
}
