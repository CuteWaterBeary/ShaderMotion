using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using AsyncGPUReadback = UnityEngine.Rendering.AsyncGPUReadback;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class GPUReader {
	Queue<AsyncGPUReadbackRequest> requests = new Queue<AsyncGPUReadbackRequest>();
	public AsyncGPUReadbackRequest? Request(Texture tex) {
		AsyncGPUReadbackRequest? request = null;
		while(requests.Count > 0) {
			var r = requests.Peek();
			if(!r.done)
				break;
			request = requests.Dequeue();
		}
		if(requests.Count < 2)
			requests.Enqueue(AsyncGPUReadback.Request(tex));
		return request;
	}
}
}