using UnityEngine;
using UnityEditor;

namespace EarthOnline.Editor
{
    /// <summary>
    /// 程序化角色生成 V2 —— 不再是胶囊体。
    /// 身体+头部+四肢+服饰+装饰+武器+特效光环。
    /// 生成可辨识的修真角色。
    /// </summary>
    public static class CharacterBuilder
    {
        public static GameObject BuildNPC(string name, Color skinColor, Color clothColor, string role)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;

            // === BODY ===
            var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Torso"; torso.transform.SetParent(root.transform);
            torso.transform.localPosition = new Vector3(0, 1.2f, 0);
            torso.transform.localScale = new Vector3(0.6f, 0.7f, 0.4f);
            SetMaterial(torso, clothColor);
            RemoveCollider(torso);

            // === HEAD ===
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0, 1.8f, 0);
            head.transform.localScale = new Vector3(0.35f, 0.4f, 0.35f);
            SetMaterial(head, skinColor);
            RemoveCollider(head);

            // Eyes
            CreateEye(head, new Vector3(-0.1f, 0.05f, 0.3f));
            CreateEye(head, new Vector3(0.1f, 0.05f, 0.3f));

            // === ARMS ===
            var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftArm.name = "LeftArm"; leftArm.transform.SetParent(root.transform);
            leftArm.transform.localPosition = new Vector3(-0.45f, 1.2f, 0);
            leftArm.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
            SetMaterial(leftArm, clothColor);
            RemoveCollider(leftArm);

            var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightArm.name = "RightArm"; rightArm.transform.SetParent(root.transform);
            rightArm.transform.localPosition = new Vector3(0.45f, 1.2f, 0);
            rightArm.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
            SetMaterial(rightArm, clothColor);
            RemoveCollider(rightArm);

            // === LEGS ===
            var leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftLeg.name = "LeftLeg"; leftLeg.transform.SetParent(root.transform);
            leftLeg.transform.localPosition = new Vector3(-0.15f, 0.5f, 0);
            leftLeg.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            SetMaterial(leftLeg, clothColor * 0.7f);
            RemoveCollider(leftLeg);

            var rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightLeg.name = "RightLeg"; rightLeg.transform.SetParent(root.transform);
            rightLeg.transform.localPosition = new Vector3(0.15f, 0.5f, 0);
            rightLeg.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            SetMaterial(rightLeg, clothColor * 0.7f);
            RemoveCollider(rightLeg);

            // === ROLE COSTUME ===
            switch (role)
            {
                case "elder": // 长者——长袍+胡须+发髻
                    AddBeard(head);
                    AddRobe(torso, clothColor);
                    AddHairBun(head);
                    break;
                case "merchant": // 商人——帽子+钱袋+圆肚
                    AddHat(head, new Color(0.6f, 0.3f, 0.1f));
                    torso.transform.localScale = new Vector3(0.7f, 0.8f, 0.5f);
                    break;
                case "guard": // 守卫——头盔+肩甲+盾
                    AddHelmet(head);
                    AddShoulderPad(root, -0.55f, new Color(0.3f, 0.3f, 0.4f));
                    AddShoulderPad(root, 0.55f, new Color(0.3f, 0.3f, 0.4f));
                    break;
                case "healer": // 医者——发髻+药袋+绿色腰带
                    AddHairBun(head);
                    var belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    belt.name = "Belt"; belt.transform.SetParent(torso.transform);
                    belt.transform.localPosition = Vector3.zero;
                    belt.transform.localScale = new Vector3(1.1f, 0.15f, 1.1f);
                    SetMaterial(belt, new Color(0.2f, 0.6f, 0.3f));
                    RemoveCollider(belt);
                    break;
                case "warrior": // 战士——铠甲+护腕
                    SetMaterial(torso, new Color(0.3f, 0.3f, 0.4f));
                    AddShoulderPad(root, -0.55f, new Color(0.3f, 0.3f, 0.4f));
                    AddShoulderPad(root, 0.55f, new Color(0.3f, 0.3f, 0.4f));
                    break;
                case "peasant": // 平民——简单布衣
                    SetMaterial(torso, new Color(0.6f, 0.5f, 0.3f));
                    break;
            }

            // === SPIRIT AURA (修仙者光环) ===
            if (role != "peasant")
            {
                var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aura.name = "Aura"; aura.transform.SetParent(root.transform);
                aura.transform.localPosition = new Vector3(0, 1.2f, 0);
                aura.transform.localScale = Vector3.one * 1.8f;
                var am = new Material(Shader.Find("Standard"));
                am.color = new Color(0.2f, 0.5f, 0.9f, 0.1f);
                am.EnableKeyword("_EMISSION");
                am.SetColor("_EmissionColor", new Color(0.2f, 0.5f, 0.9f) * 0.3f);
                aura.GetComponent<Renderer>().material = am;
                RemoveCollider(aura);
            }

            // Add CharacterController for movement
            root.AddComponent<UnityEngine.CharacterController>();
            return root;
        }

        // ====================================================================
        // BuildNPC(NPCVisualConfig) —— 基于 NPCVisualDatabase 配置生成角色
        // ====================================================================
        public static GameObject BuildNPC(NPCVisualConfig config)
        {
            // NPCVisualRole → 内部 role 字符串映射
            string role = config.roleType switch
            {
                NPCVisualRole.Elder => "elder",
                NPCVisualRole.Merchant => "merchant",
                NPCVisualRole.Guard => "guard",
                NPCVisualRole.Healer => "healer",
                NPCVisualRole.Warrior => "warrior",
                NPCVisualRole.Peasant => "peasant",
                NPCVisualRole.FemaleScholar => "warrior",
                NPCVisualRole.Master => "elder",
                NPCVisualRole.Drunkard => "peasant",
                NPCVisualRole.Child => "peasant",
                _ => "peasant"
            };

            var root = new GameObject(config.name);
            root.transform.position = Vector3.zero;
            root.transform.localScale = new Vector3(config.widthScale, config.heightScale, config.widthScale);

            // === BODY ===
            var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "Torso"; torso.transform.SetParent(root.transform);
            torso.transform.localPosition = new Vector3(0, 1.2f, 0);
            torso.transform.localScale = new Vector3(0.6f, 0.7f, 0.4f);
            SetMaterial(torso, config.clothColor);
            RemoveCollider(torso);

            // === HEAD ===
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0, 1.8f, 0);
            head.transform.localScale = new Vector3(0.35f, 0.4f, 0.35f);
            SetMaterial(head, config.skinColor);
            RemoveCollider(head);

            // Eyes
            CreateEye(head, new Vector3(-0.1f, 0.05f, 0.3f));
            CreateEye(head, new Vector3(0.1f, 0.05f, 0.3f));

            // === ARMS ===
            var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftArm.name = "LeftArm"; leftArm.transform.SetParent(root.transform);
            leftArm.transform.localPosition = new Vector3(-0.45f, 1.2f, 0);
            leftArm.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
            SetMaterial(leftArm, config.clothColor);
            RemoveCollider(leftArm);

            var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightArm.name = "RightArm"; rightArm.transform.SetParent(root.transform);
            rightArm.transform.localPosition = new Vector3(0.45f, 1.2f, 0);
            rightArm.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
            SetMaterial(rightArm, config.clothColor);
            RemoveCollider(rightArm);

            // === LEGS ===
            var leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftLeg.name = "LeftLeg"; leftLeg.transform.SetParent(root.transform);
            leftLeg.transform.localPosition = new Vector3(-0.15f, 0.5f, 0);
            leftLeg.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            SetMaterial(leftLeg, config.clothColor * 0.7f);
            RemoveCollider(leftLeg);

            var rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightLeg.name = "RightLeg"; rightLeg.transform.SetParent(root.transform);
            rightLeg.transform.localPosition = new Vector3(0.15f, 0.5f, 0);
            rightLeg.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);
            SetMaterial(rightLeg, config.clothColor * 0.7f);
            RemoveCollider(rightLeg);

            // === ROLE COSTUME ===
            switch (role)
            {
                case "elder":
                    AddBeard(head);
                    AddRobe(torso, config.clothColor);
                    AddHairBun(head);
                    break;
                case "merchant":
                    AddHat(head, config.accentColor);
                    torso.transform.localScale = new Vector3(0.7f, 0.8f, 0.5f);
                    break;
                case "guard":
                    AddHelmet(head);
                    AddShoulderPad(root, -0.55f, config.accentColor);
                    AddShoulderPad(root, 0.55f, config.accentColor);
                    break;
                case "healer":
                    AddHairBun(head);
                    var belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    belt.name = "Belt"; belt.transform.SetParent(torso.transform);
                    belt.transform.localPosition = Vector3.zero;
                    belt.transform.localScale = new Vector3(1.1f, 0.15f, 1.1f);
                    SetMaterial(belt, config.accentColor);
                    RemoveCollider(belt);
                    break;
                case "warrior":
                    SetMaterial(torso, config.accentColor);
                    AddShoulderPad(root, -0.55f, config.accentColor);
                    AddShoulderPad(root, 0.55f, config.accentColor);
                    break;
                case "peasant":
                    SetMaterial(torso, config.clothColor);
                    break;
            }

            // === SPIRIT AURA ===
            if (config.hasAura)
            {
                var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aura.name = "Aura"; aura.transform.SetParent(root.transform);
                aura.transform.localPosition = new Vector3(0, 1.2f, 0);
                aura.transform.localScale = Vector3.one * 1.8f;
                var am = new Material(Shader.Find("Standard"));
                am.color = new Color(config.auraColor.r, config.auraColor.g, config.auraColor.b, 0.1f);
                am.EnableKeyword("_EMISSION");
                am.SetColor("_EmissionColor", config.auraColor * 0.3f);
                aura.GetComponent<Renderer>().material = am;
                RemoveCollider(aura);
            }

            root.AddComponent<UnityEngine.CharacterController>();
            return root;
        }

        static void CreateEye(GameObject head, Vector3 localPos)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye"; eye.transform.SetParent(head.transform);
            eye.transform.localPosition = localPos;
            eye.transform.localScale = Vector3.one * 0.08f;
            SetMaterial(eye, Color.white);
            RemoveCollider(eye);
            var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil"; pupil.transform.SetParent(eye.transform);
            pupil.transform.localPosition = Vector3.zero;
            pupil.transform.localScale = Vector3.one * 0.5f;
            SetMaterial(pupil, Color.black);
            RemoveCollider(pupil);
        }

        static void AddBeard(GameObject head)
        {
            var beard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beard.name = "Beard"; beard.transform.SetParent(head.transform);
            beard.transform.localPosition = new Vector3(0, -0.3f, 0.15f);
            beard.transform.localScale = new Vector3(0.25f, 0.15f, 0.03f);
            SetMaterial(beard, Color.white);
            RemoveCollider(beard);
        }

        static void AddHairBun(GameObject head)
        {
            var bun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bun.name = "HairBun"; bun.transform.SetParent(head.transform);
            bun.transform.localPosition = new Vector3(0, 0.3f, -0.05f);
            bun.transform.localScale = Vector3.one * 0.2f;
            SetMaterial(bun, Color.black);
            RemoveCollider(bun);
        }

        static void AddHat(GameObject head, Color color)
        {
            var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brim.name = "HatBrim"; brim.transform.SetParent(head.transform);
            brim.transform.localPosition = new Vector3(0, 0.25f, 0);
            brim.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
            SetMaterial(brim, color);
            RemoveCollider(brim);
            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "HatTop"; top.transform.SetParent(brim.transform);
            top.transform.localPosition = new Vector3(0, 0.8f, 0);
            top.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f);
            SetMaterial(top, color);
            RemoveCollider(top);
        }

        static void AddHelmet(GameObject head)
        {
            var helm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            helm.name = "Helmet"; helm.transform.SetParent(head.transform);
            helm.transform.localPosition = new Vector3(0, 0.1f, 0);
            helm.transform.localScale = new Vector3(0.4f, 0.15f, 0.4f);
            SetMaterial(helm, new Color(0.3f, 0.3f, 0.4f));
            RemoveCollider(helm);
        }

        static void AddShoulderPad(GameObject root, float xPos, Color color)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pad.name = "ShoulderPad"; pad.transform.SetParent(root.transform);
            pad.transform.localPosition = new Vector3(xPos, 1.45f, 0);
            pad.transform.localScale = new Vector3(0.25f, 0.25f, 0.2f);
            SetMaterial(pad, color);
            RemoveCollider(pad);
        }

        static void AddRobe(GameObject torso, Color color)
        {
            var robe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            robe.name = "Robe"; robe.transform.SetParent(torso.transform);
            robe.transform.localPosition = new Vector3(0, -0.2f, 0);
            robe.transform.localScale = new Vector3(1.2f, 0.8f, 1.1f);
            SetMaterial(robe, color * 0.85f);
            RemoveCollider(robe);
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
                m.SetColor("_EmissionColor", color * 0.05f);
            }
            r.material = m;
        }

        static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        // === ENEMY BUILDER ===
        public static GameObject BuildEnemy(string name, string type, Color color, float scale)
        {
            var root = new GameObject(name);

            Color bodyColor = type switch
            {
                "beast" => color,
                "spirit" => new Color(color.r, color.g, color.b, 0.5f),
                "dragon" => color,
                "undead" => new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f),
                _ => color
            };

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body"; body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0, 1f * scale, 0);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f) * scale;
            SetMaterial(body, bodyColor);

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(root.transform);

            if (type == "dragon")
            {
                // Dragon head shape
                head.transform.localPosition = new Vector3(0, 1.8f * scale, 0.5f * scale);
                head.transform.localScale = new Vector3(0.4f, 0.3f, 0.6f) * scale;
                // Horns
                var hornL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hornL.name = "HornL"; hornL.transform.SetParent(head.transform);
                hornL.transform.localPosition = new Vector3(-0.3f, 0.5f, 0);
                hornL.transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);
                SetMaterial(hornL, Color.white);
                RemoveCollider(hornL);
                var hornR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hornR.name = "HornR"; hornR.transform.SetParent(head.transform);
                hornR.transform.localPosition = new Vector3(0.3f, 0.5f, 0);
                hornR.transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);
                SetMaterial(hornR, Color.white);
                RemoveCollider(hornR);
            }
            else if (type == "spirit")
            {
                head.transform.localPosition = new Vector3(0, 1.7f * scale, 0);
                SetMaterial(head, new Color(color.r, color.g, color.b, 0.3f));
            }
            else
            {
                head.transform.localPosition = new Vector3(0, 1.7f * scale, 0);
                head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f) * scale;
                SetMaterial(head, bodyColor);
                // Eyes
                CreateEye(head, new Vector3(-0.15f, 0.05f, 0.3f));
                CreateEye(head, new Vector3(0.15f, 0.05f, 0.3f));
            }

            // Enemy glow
            var r = body.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.EnableKeyword("_EMISSION");
                float glow = type == "dragon" ? 0.6f : type == "spirit" ? 0.8f : 0.2f;
                r.material.SetColor("_EmissionColor", color * glow);
            }

            root.AddComponent<UnityEngine.CharacterController>();
            return root;
        }
    }
}
