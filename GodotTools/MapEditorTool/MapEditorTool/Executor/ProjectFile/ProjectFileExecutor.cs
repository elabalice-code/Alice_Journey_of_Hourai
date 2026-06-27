using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using MapEditorTool.Models;

namespace MapEditorTool.Executor.ProjectFile
{
    public sealed class ProjectFileExecutor
    {
        public MapProject LoadProject(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new FileNotFoundException("Project file path is empty.", filePath);
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Project file not found.", filePath);

            using (var stream = OpenJsonReadStream(filePath))
            {
                var serializer = new DataContractJsonSerializer(typeof(MapProject), BuildSettings());
                var loaded = serializer.ReadObject(stream) as MapProject;
                if (loaded == null)
                    throw new InvalidDataException("Failed to parse MapProject JSON.");
                NormalizeProject(loaded);
                return loaded;
            }
        }

        public void SaveProject(string filePath, MapProject project)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new FileNotFoundException("Project file path is empty.", filePath);
            if (project == null)
                throw new InvalidDataException("Cannot save a null MapProject.");

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(MapProject), BuildSettings());
                serializer.WriteObject(stream, project);
                var json = Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(fullPath, json, new UTF8Encoding(false));
            }
        }

        private static Stream OpenJsonReadStream(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new MemoryStream(bytes, 3, bytes.Length - 3);

            return new MemoryStream(bytes);
        }

        private static DataContractJsonSerializerSettings BuildSettings()
        {
            return new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            };
        }

        private static void NormalizeProject(MapProject project)
        {
            if (project.Maps == null)
                project.Maps = new System.Collections.Generic.List<MapDefinition>();
            if (project.Links == null)
                project.Links = new System.Collections.Generic.List<MapLink>();
        }
    }
}
