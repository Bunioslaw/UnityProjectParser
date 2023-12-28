using YamlDotNet.RepresentationModel;

public class Transform
{
  public Transform(string objectId)
  {
    this.objectId = objectId;
    this.children_Ids = new List<string>();
  }
  public string objectId { get; set; }
  public List<string> children_Ids { get; set; }
}

public class UnityParser
{
  List<string> usedScripts = new List<string>();
  Dictionary<string, string> gameObject_names = new Dictionary<string, string>(); //gameObjectId, objectName
  Dictionary<string, Transform> transforms = new Dictionary<string, Transform>(); //transformId, transformObj
  YamlMappingNode sceneRoots = new YamlMappingNode();
  private string outputPath = string.Empty;
  private string projectPath = string.Empty;

  public UnityParser(string outputPath, string projectPath)
  {
    this.projectPath = projectPath;
    this.outputPath = outputPath;
  }

  public void parseScenes()
  {
    string scenesPath = projectPath + "\\Assets\\Scenes";
    string[] scenes = Directory.GetFiles(scenesPath, "*.unity", SearchOption.TopDirectoryOnly);
    foreach (string sceneFile in scenes)
    {
      this.parseScene(sceneFile);
    }
  }

  private void parseScene(string filePath)
  {
    //Load file into yamlStream
    var input = new StreamReader(filePath);
    string fileName = Path.GetFileName(filePath) + ".dump";

    var yaml = new YamlStream();
    yaml.Load(input);

    //Iterate through every tag and remeber it if its gameobject or transform
    //Also check for used scripts
    foreach (var mapping in yaml.Documents)
    {
      var map = (YamlMappingNode)mapping.RootNode;
      string mapType = map.Children[0].Key.ToString();
      if (mapType == "GameObject")
      {
        string key = map.Anchor.ToString();
        string name = map["GameObject"]["m_Name"].ToString();
        gameObject_names.Add(key, name);
      }
      else if (mapType == "Transform")
      {
        string key = map.Anchor.ToString();
        string objId = map["Transform"]["m_GameObject"]["fileID"].ToString();
        transforms.Add(key, new Transform(objId));
        foreach (var node in (YamlSequenceNode)map["Transform"]["m_Children"])
        {
          transforms[key].children_Ids.Add(node["fileID"].ToString());
        }
      }
      else if (mapType == "SceneRoots")
      {
        sceneRoots = map;
      }
      else if (mapType == "MonoBehaviour")
      {
        string scriptId = map["MonoBehaviour"]["m_Script"]["guid"].ToString();
        usedScripts.Add(scriptId);
      }
    }

    string text = string.Empty;
    //Print out gameObjects in order of sceneRoots
    foreach (var node in (YamlSequenceNode)sceneRoots["SceneRoots"]["m_Roots"])
    {
      string transformId = node["fileID"].ToString();
      text += Print(transforms[transformId], 0);
    }
    string outputDir = outputPath + "\\" + fileName;
    Directory.CreateDirectory(outputPath);
    File.WriteAllText(outputDir, text);
  }

  private string Print(Transform transform, int indent)
  {
    string text = "";
    for (int i = 0; i < indent; i++)
    {
      text += "--";
    }
    text += gameObject_names[transform.objectId] + "\n";
    foreach (string transId in transform.children_Ids)
    {
      Transform trans = transforms[transId];
      text += Print(trans, indent + 1);
    }
    return text;
  }

  private bool checkScriptUsage(string guid)
  {
    foreach (string id in usedScripts)
    {
      if (id == guid)
        return true;
    }
    return false;
  }

  private string unusedScriptTextFormat(string scriptPath, string guid)
  {
    string text = string.Empty;
    int start = projectPath.Length + 1;
    string relativePath = scriptPath.Substring(start, scriptPath.Length - 5 - start);
    text = relativePath + ',' + guid + '\n';
    return text;
  }

  public void parseScripts(string projectPath)
  {
    string scriptsPath = projectPath + "\\Assets\\Scripts";
    string[] scripts = Directory.GetFiles(scriptsPath, "*.cs.meta", SearchOption.AllDirectories);
    string text = "Relative Path,GUID\n";
    foreach (string scriptPath in scripts)
    {
      //Load file into yamlStream
      var input = new StreamReader(scriptPath);

      var yaml = new YamlStream();
      yaml.Load(input);

      foreach (var mapping in yaml.Documents)
      {
        var map = (YamlMappingNode)mapping.RootNode;
        foreach (var node in map.Children)
        {
          if (node.Key.ToString() == "guid")
          {
            string guid = node.Value.ToString();
            if (!this.checkScriptUsage(guid))
            {
              text += unusedScriptTextFormat(scriptPath, guid);
            }
          }
        }
      }
    }
    string outputDir = outputPath + "\\UnusedScripts.csv";
    Directory.CreateDirectory(outputPath);
    File.WriteAllText(outputDir, text);
  }
}

internal class Program
{
  private static void Main(string[] args)
  {
    string projectPath = args[0];
    string outputPath = args[1];

    var unityParser = new UnityParser(outputPath, projectPath);

    unityParser.parseScenes();
    unityParser.parseScripts(projectPath);
  }
}