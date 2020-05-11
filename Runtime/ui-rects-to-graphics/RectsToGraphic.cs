using BeatThat.GetComponentsExt;
using BeatThat.Pools;
using BeatThat.Rects;
using BeatThat.SafeRefs;
using BeatThat.UIGeometryUtils;
using BeatThat.UIRectTransformEvents;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BeatThat.UIRectsToGraphics
{
	public class RectsToGraphic : Graphic 
	{
		public List<RectTransform> m_rects;

		[Tooltip("set TRUE connect the rects into a single contiguous graphic")]
		public bool m_connectEnds = true;
		public bool m_debugVerts = false;

		[Tooltip("set TRUE to block raycasts everywhere EXCEPT the rect areas.")]
		public bool m_raycastsPassThroughRects = true;

		private IList<RectTransform> rects { get { return m_rects; } }

		public void SetRects(IEnumerable<RectTransform> rects)
		{
			var localRects = m_rects?? (m_rects = new List<RectTransform>());
			localRects.Clear();
			localRects.AddRange(rects);
			UpdateRects();
		}

		override public bool Raycast(Vector2 sp, Camera eventCamera)
		{
			if(!this.raycastTarget) {
				return false;
			}
			if(!RectTransformUtility.RectangleContainsScreenPoint(this.rectTransform, sp, eventCamera)) {
				// if the screen point doesn't hit the container rect, then ignore it
				return false;
			}
			if(!m_raycastsPassThroughRects) {
				return true;
			}
			var rectList = this.rectRefs;
			if(rectList == null) {
				return true;
			}
			foreach(var r in rectList) {
				if(r.value == null) {
					continue;
				}
				if(RectTransformUtility.RectangleContainsScreenPoint(r.value, sp, eventCamera)) {
					return false;
				}
			}
			return true;
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			if(m_debugVerts) {
				using(vh.DebugStart()) {
					DoOnPopulateMesh(vh);
				}
			}
			else {
				DoOnPopulateMesh(vh);
			}
		}

		private void DoOnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			var thisRT = this.rectTransform;
			var thisRect = this.rectTransform.rect;
			int vOff = 0;
			var rectArray = this.rectRefs;
			if(rectArray == null || rectArray.Count == 0) {
				return;
			}
			using(var quad = ArrayPool<UIVertex>.Get(4)) {
				Rect prevRect = default(Rect);
				for(var i = 0; i < rectArray.Count; i++) {
					var c = rectArray[i].value;
					if(c == null) {
						continue; 
					}
					var curRect = thisRT.InverseTransformRect(c);
					if(i > 0 && m_connectEnds) {
						var uiVert = UIVertex.simpleVert;
						uiVert.color = this.color;
						var zPos = thisRT.position.z;
						uiVert.position = new Vector3(prevRect.xMax, prevRect.yMin, zPos);
						uiVert.uv0 = new Vector2(0, 0);
						quad.array[0] = uiVert;
						uiVert.position = new Vector3(prevRect.xMax, prevRect.yMax, zPos);
						uiVert.uv0 = new Vector2(0, 1);
						quad.array[1] = uiVert;
						uiVert.position = new Vector3(curRect.xMin, curRect.yMax, zPos);
						uiVert.uv0 = new Vector2(1, 1);
						quad.array[2] = uiVert;
						uiVert.position = new Vector3(curRect.xMin, curRect.yMin, zPos);
						uiVert.uv0 = new Vector2(1, 0);
						quad.array[3] = uiVert;
						vOff += vh.AddQuadClipped(quad.array, thisRect, vOff);
					}
					vOff += vh.AddRectClipped(curRect, this.color, thisRect, vOff);
					prevRect = curRect;
				}
			}
		}


		private void UnregisterRects()
		{
			var list = this.rectRefs;
			if(list == null) {
				return;
			}
			for(int i = 0; i < list.Count; i++) {
				var c = list[i].value;
				if(c == null) {
					continue;
				}
				var e = c.GetComponent<RectTransformEvents>();
				if(e == null) {
					continue;
				}
				e.onScreenRectChanged.RemoveListener(this.onRectUpdatedAction); 
			}
		}

		private void UpdateRects()
		{
			UnregisterRects();
			var list = this.rectRefs;
			if(list == null) {
				list = new List<SafeRef<RectTransform>>();
			}
			else {
				list.Clear();
			}
			foreach (var c in this.rects) {
				list.Add (new SafeRef<RectTransform> (c));
				if (c == null) {
					continue;
				}
				var e = c.AddIfMissing<RectTransformEvents> ();
				e.onScreenRectChanged.RemoveListener (this.onRectUpdatedAction);
				e.onScreenRectChanged.AddListener (this.onRectUpdatedAction);
			}
			this.rectRefs = list;
			SetVerticesDirty();
		}

		private void OnRectUpdated()
		{
			SetVerticesDirty();
		}
		private UnityAction onRectUpdatedAction { get { return m_onRectUpdatedAction?? (m_onRectUpdatedAction = this.OnRectUpdated); } }
		private UnityAction m_onRectUpdatedAction;

#if UNITY_EDITOR
		override protected void OnValidate()
		{
			UpdateRects();
			base.OnValidate();
		}
#endif

		override protected void OnDestroy()
		{
			UnregisterRects();
			base.OnDestroy();
		}

		override protected void OnEnable()
		{
			UpdateRects();
			base.OnEnable();
		}

		void OnDrawGizmosSelected()
		{
			var rectList = this.rectRefs;
			if(rectList == null) {
				return;
			}
			var saveColor = Gizmos.color;
			foreach(var c in rectList) {
				if(c.value ==  null) {
					continue;
				}
				var r = c.value.TransformRect(c.value.rect);
				var fillColor = Color.cyan;
				fillColor.a = 0.15f;
				Gizmos.color = fillColor;
				Gizmos.DrawCube((Vector3)r.center, new Vector3(r.width, r.height));
				Gizmos.color = Color.blue;
				Gizmos.DrawLine((Vector3)r.min, new Vector3(r.xMin, r.yMax));
				Gizmos.DrawLine(new Vector3(r.xMin, r.yMax), (Vector3)r.max);
				Gizmos.DrawLine((Vector3)r.max, new Vector3(r.xMax, r.yMin));
				Gizmos.DrawLine(new Vector3(r.xMax, r.yMin), (Vector3)r.min);
			}
			Gizmos.color = saveColor;
		}

		private List<SafeRef<RectTransform>> rectRefs { get; set; }
	}
}





