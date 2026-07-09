using UnityEngine;
using UnityEditor;

namespace EarthOnline.Editor
{
    /// <summary>
    /// 程序化角色生成 —— 从胶囊体升级到组合模型。
    /// 每个角色由多个Primitive组成：身体+头部+服饰+武器+特效。
    /// 不是从零建模——是用程序搭出可辨识的角色。
    /// </summary>
    public static class CharacterBuilder
    {
        public static GameObject BuildNPC(string name, Color skinColor, Color clothColor, string role)
        {
            var root = new GameObject(name);

            // 身体（胶囊）
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body"; body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0, 1, 0);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            SetMaterial(body, clothColor);

            // 头部（球体）
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0, 1.8f, 0);
            head.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            SetMaterial(head, skinColor);

            // 眼睛（两个小球）
            var leftEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftEye.name = "LeftEye"; leftEye.transform.SetParent(head.transform);
            leftEye.transform.localPosition = new Vector3(-0.15f, 0.05f, 0.35f);
            leftEye.transform.localScale = Vector3.one * 0.15f;
            SetMaterial(leftEye, Color.white);
            var pupilL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupilL.name = "PupilL"; pupilL.transform.SetParent(leftEye.transform);
            pupilL.transform.localPosition = Vector3.zero; pupilL.transform.localScale = Vector3.one * 0.5f;
            SetMaterial(pupilL, Color.black);

            var rightEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightEye.name = "RightEye"; rightEye.transform.SetParent(head.transform);
            rightEye.transform.localPosition = new Vector3(0.15f, 0.05f, 0.35f);
            rightEye.transform.localScale = Vector3.one * 0.15f;
            SetMaterial(rightEye, Color.white);
            var pupilR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupilR.name = "PupilR"; pupilR.transform.SetParent(rightEye.transform);
            pupilR.transform.localPosition = Vector3.zero; pupilR.transform.localScale = Vector3.one * 0.5f;
            SetMaterial(pupilR, Color.black);

            // 角色特定装饰
            switch (role)
            {
                case "elder": // 长者——胡子+拐杖
                    var beard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    beard.name = "Beard"; beard.transform.SetParent(head.transform);
                    beard.transform.localPosition = new Vector3(0, -0.3f, 0.15f);
                    beard.transform.localScale = new Vector3(0.3f, 0.2f, 0.05f);
                    SetMaterial(beard, Color.white);
                    break;
                case "merchant": // 商人——帽子
                    var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    hat.name = "Hat"; hat.transform.SetParent(head.transform);
                    hat.transform.localPosition = new Vector3(0, 0.3f, 0);
                    hat.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
                    SetMaterial(hat, new Color(0.6f, 0.3f, 0.1f));
                    break;
                case "guard": // 守卫——头盔
                    var helm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    helm.name = "Helm"; helm.transform.SetParent(head.transform);
                    helm.transform.localPosition = new Vector3(0, 0.25f, 0);
                    helm.transform.localScale = new Vector3(0.45f, 0.1f, 0.45f);
                    SetMaterial(helm, new Color(0.3f, 0.3f, 0.4f));
                    break;
                case "healer": // 医者——发髻
                    var bun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bun.name = "Bun"; bun.transform.SetParent(head.transform);
                    bun.transform.localPosition = new Vector3(0, 0.3f, -0.1f);
                    bun.transform.localScale = Vector3.one * 0.25f;
                    SetMaterial(bun, Color.black);
                    break;
            }

            return root;
        }

        static void SetMaterial(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var m = new Material(Shader.Find("Standard"));
            m.color = color;
            if (color != Color.white && color != Color.black)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.1f);
            }
            r.material = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
