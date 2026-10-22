using NUnit.Framework;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    public class RuntimeComponentTests
    {
        [Test]
        public void AddMarker_KeepsFields()
        {
            var go = new GameObject("n");
            var m = go.AddComponent<SeedanceUiElement>();
            m.elementId = "e3";
            m.sourceName = "角色属性";
            m.type = "text";
            Assert.AreEqual("e3", go.GetComponent<SeedanceUiElement>().elementId);
            Assert.AreEqual("角色属性", go.GetComponent<SeedanceUiElement>().sourceName);
            Object.DestroyImmediate(go);
        }
    }
}
