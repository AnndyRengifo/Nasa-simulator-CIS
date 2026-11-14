using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sol3.Cinematics.EditorTools
{
    /// <summary>Informe de configuración / validación que se muestra en la consola y se guarda en Logs/.</summary>
    internal sealed class CinematicReport
    {
        public enum Severity
        {
            Ok,
            Info,
            Pending,
            Warning,
            Error,
        }

        public readonly struct Entry
        {
            public readonly Severity Severity;
            public readonly string Category;
            public readonly string Message;

            public Entry(Severity severity, string category, string message)
            {
                Severity = severity;
                Category = category;
                Message = message;
            }
        }

        readonly List<Entry> m_Entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => m_Entries;

        public void Ok(string category, string message) => Add(Severity.Ok, category, message);
        public void Info(string category, string message) => Add(Severity.Info, category, message);
        public void Pending(string category, string message) => Add(Severity.Pending, category, message);
        public void Warning(string category, string message) => Add(Severity.Warning, category, message);
        public void Error(string category, string message) => Add(Severity.Error, category, message);

        public void Add(Severity severity, string category, string message) =>
            m_Entries.Add(new Entry(severity, category, message));

        public int Count(Severity severity)
        {
            int count = 0;
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_Entries[i].Severity == severity)
                    count++;
            }
            return count;
        }

        public string Summary =>
            $"{Count(Severity.Ok)} OK · {Count(Severity.Pending)} pendientes · {Count(Severity.Warning)} avisos · {Count(Severity.Error)} errores";

        public string ToText(string title)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"=== {title} — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            builder.AppendLine($"Unity {Application.unityVersion} · {Summary}");
            string category = null;
            foreach (var entry in m_Entries)
            {
                if (entry.Category != category)
                {
                    category = entry.Category;
                    builder.AppendLine();
                    builder.AppendLine($"[{category}]");
                }
                builder.AppendLine($"  {Tag(entry.Severity)} {entry.Message}");
            }
            return builder.ToString();
        }

        /// <summary>Escribe el informe en la consola (un único mensaje) y en Logs/&lt;fileName&gt;.</summary>
        public string Publish(string title, string fileName)
        {
            string text = ToText(title);
            string path = Path.Combine("Logs", fileName);
            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Cinematics] No se pudo guardar el informe en {path}: {exception.Message}");
            }

            string message = $"[Cinematics] {title}: {Summary}  (informe completo en {path})\n\n{text}";
            if (Count(Severity.Error) > 0)
                Debug.LogError(message);
            else if (Count(Severity.Warning) > 0)
                Debug.LogWarning(message);
            else
                Debug.Log(message);
            return path;
        }

        static string Tag(Severity severity)
        {
            switch (severity)
            {
                case Severity.Ok: return "[OK]       ";
                case Severity.Info: return "[INFO]     ";
                case Severity.Pending: return "[PENDIENTE]";
                case Severity.Warning: return "[AVISO]    ";
                default: return "[ERROR]    ";
            }
        }
    }
}
