#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Path = System.IO.Path;
using Array = System.Array;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public class WebExporter {
	static readonly MethodInfo OpenCompiledShader = typeof(ShaderUtil).GetMethod("OpenCompiledShader", BindingFlags.NonPublic | BindingFlags.Static);
	static readonly int platformMaskWebGL = ((UnityEditor.Rendering.ShaderCompilerPlatform[])
												System.Enum.GetValues(typeof(UnityEditor.Rendering.ShaderCompilerPlatform)))
												.Where(i => i.ToString().StartsWith("GLES3")).Sum(i => 1<<(int)i);
	static void CompileWebGL(Shader shader, string path) {
		if(!System.IO.Directory.Exists(Path.GetDirectoryName(path)))
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

		const int mode = 3; // Custom mode
		const bool includeAllVariants = false;
		OpenCompiledShader.Invoke(null, new object[] { shader, mode, platformMaskWebGL, includeAllVariants});

		var tempPath = $"Temp/Compiled-{shader.name.Replace('/', '-')}.shader";
		var text = System.IO.File.ReadAllText(tempPath);
		var matches = re_VERTEX_FRAGMENT.Matches(text);
		foreach (Match match in matches) {
			var vs = FixShaderSource(match.Groups[1].ToString());
			var fs = FixShaderSource(match.Groups[2].ToString());
			System.IO.File.WriteAllText(path, $"export default {{\nvs: `{vs}`,\nfs: `{fs}`}};");
			break;
		}
	}

	static readonly Regex re_VERTEX_FRAGMENT = new Regex(
		@"#ifdef VERTEX\s+(.+?)\s+#endif\s+#ifdef FRAGMENT\s+(.+?)\s+#endif\s+--",
			RegexOptions.Compiled | RegexOptions.Singleline);
	static readonly Regex re_vec4_hlslcc_mtx4x4 = new Regex(@"vec4 hlslcc_mtx4x4(\w+)\[4\]", RegexOptions.Compiled);
	static readonly Regex re_UNITY_ADRENO_ES3 = new Regex(@"[#]ifdef UNITY_ADRENO_ES3[^#]+[#]else\n([^#]+)[#]endif\n", RegexOptions.Compiled);
	static readonly Regex re_ivec3 = new Regex(@"ivec3(\(\d+, \d+\))", RegexOptions.Compiled);
	static string FixShaderSource(string source) {
		// take mat4 instead of vec4[4]
		source = re_vec4_hlslcc_mtx4x4.Replace(source, "mat4 $1"); 
		source = source.Replace("hlslcc_mtx4x4", "");
		// remove UNITY_ADRENO_ES3 branch
		source = re_UNITY_ADRENO_ES3.Replace(source, "$1");
		// fix hlslcc bug: wrong constructor ivec3(x, y) in texelFetchOffset
		source = re_ivec3.Replace(source, "ivec2$1");
		return source;
	}

	[MenuItem("CONTEXT/Shader/CompileWebGL")]
	static void CompileWebGL(MenuCommand command) {
		var shader = (Shader)command.context;
		var path = AssetDatabase.GetAssetPath(shader);
		path = Path.Combine(Path.GetDirectoryName(path), "auto",
					Path.GetFileNameWithoutExtension(path)+".js");
		CompileWebGL(shader, path);
	}
}
}
#endif