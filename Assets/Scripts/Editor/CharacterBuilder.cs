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
                NPCVisualRole.FemaleScholar => "femalescholar",
                NPCVisualRole.Master => "master",
                NPCVisualRole.Drunkard => "drunkard",
                NPCVisualRole.Child => "child",
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
                case "femalescholar":
                    // 纤细体型
                    torso.transform.localScale = new Vector3(0.45f, 0.65f, 0.35f);
                    leftArm.transform.localScale = new Vector3(0.09f, 0.5f, 0.09f);
                    rightArm.transform.localScale = new Vector3(0.09f, 0.5f, 0.09f);
                    leftLeg.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
                    rightLeg.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
                    // 双发髻
                    AddTwinBuns(head);
                    // 香囊配饰
                    AddSachet(torso, config.accentColor);
                    break;
                case "master":
                    // 最高大
                    torso.transform.localScale = new Vector3(0.8f, 0.9f, 0.55f);
                    head.transform.localScale = new Vector3(0.4f, 0.45f, 0.4f);
                    leftArm.transform.localScale = new Vector3(0.16f, 0.6f, 0.16f);
                    rightArm.transform.localScale = new Vector3(0.16f, 0.6f, 0.16f);
                    leftLeg.transform.localScale = new Vector3(0.2f, 0.55f, 0.2f);
                    rightLeg.transform.localScale = new Vector3(0.2f, 0.55f, 0.2f);
                    // 道冠
                    AddDaoCrown(head, config.accentColor);
                    // 外袍
                    AddRobe(torso, config.clothColor);
                    // 浮珠
                    AddFloatingBeads(root, config.accentColor);
                    break;
                case "drunkard":
                    // 瘦削
                    torso.transform.localScale = new Vector3(0.5f, 0.7f, 0.35f);
                    leftArm.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
                    rightArm.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
                    // 散发
                    AddScatteredHair(head);
                    // 酒葫芦
                    AddGourd(root, new Color(0.6f, 0.4f, 0.2f));
                    break;
                case "child":
                    // 最小体型
                    torso.transform.localScale = new Vector3(0.4f, 0.45f, 0.3f);
                    head.transform.localScale = new Vector3(0.25f, 0.3f, 0.25f);
                    leftArm.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
                    rightArm.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
                    leftLeg.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);
                    rightLeg.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);
                    // 单发髻
                    AddHairBun(head);
                    // 小药篮
                    AddMedicineBasket(root, new Color(0.5f, 0.35f, 0.2f));
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

        // === FEMALE SCHOLAR COSTUME HELPERS ===
        static void AddTwinBuns(GameObject head)
        {
            var leftBun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftBun.name = "HairBunL"; leftBun.transform.SetParent(head.transform);
            leftBun.transform.localPosition = new Vector3(-0.15f, 0.25f, -0.05f);
            leftBun.transform.localScale = Vector3.one * 0.15f;
            SetMaterial(leftBun, Color.black);
            RemoveCollider(leftBun);
            var rightBun = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightBun.name = "HairBunR"; rightBun.transform.SetParent(head.transform);
            rightBun.transform.localPosition = new Vector3(0.15f, 0.25f, -0.05f);
            rightBun.transform.localScale = Vector3.one * 0.15f;
            SetMaterial(rightBun, Color.black);
            RemoveCollider(rightBun);
        }

        static void AddSachet(GameObject torso, Color color)
        {
            var sachet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sachet.name = "Sachet"; sachet.transform.SetParent(torso.transform);
            sachet.transform.localPosition = new Vector3(0.2f, -0.25f, 0.25f);
            sachet.transform.localScale = Vector3.one * 0.08f;
            SetMaterial(sachet, color);
            RemoveCollider(sachet);
            var tassel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tassel.name = "Tassel"; tassel.transform.SetParent(sachet.transform);
            tassel.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            tassel.transform.localScale = new Vector3(0.02f, 0.12f, 0.02f);
            SetMaterial(tassel, color * 0.7f);
            RemoveCollider(tassel);
        }

        // === MASTER COSTUME HELPERS ===
        static void AddDaoCrown(GameObject head, Color color)
        {
            var crownBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crownBase.name = "DaoCrownBase"; crownBase.transform.SetParent(head.transform);
            crownBase.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            crownBase.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            SetMaterial(crownBase, color);
            RemoveCollider(crownBase);
            var crownTop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crownTop.name = "DaoCrownTop"; crownTop.transform.SetParent(crownBase.transform);
            crownTop.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            crownTop.transform.localScale = new Vector3(0.6f, 0.12f, 0.6f);
            SetMaterial(crownTop, color * 0.8f);
            RemoveCollider(crownTop);
            var ornament = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ornament.name = "DaoOrnament"; ornament.transform.SetParent(crownTop.transform);
            ornament.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            ornament.transform.localScale = Vector3.one * 0.08f;
            SetMaterial(ornament, new Color(0.9f, 0.7f, 0.2f));
            RemoveCollider(ornament);
        }

        static void AddFloatingBeads(GameObject root, Color color)
        {
            var positions = new Vector3[]
            {
                new Vector3(0.5f, 1.5f, 0f),
                new Vector3(-0.5f, 1.5f, 0f),
                new Vector3(0f, 1.5f, 0.5f),
                new Vector3(0f, 1.5f, -0.5f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bead.name = "FloatingBead" + i; bead.transform.SetParent(root.transform);
                bead.transform.localPosition = positions[i];
                bead.transform.localScale = Vector3.one * 0.07f;
                SetMaterial(bead, color);
                RemoveCollider(bead);
            }
        }

        // === DRUNKARD COSTUME HELPERS ===
        static void AddScatteredHair(GameObject head)
        {
            var messy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            messy.name = "MessyHair"; messy.transform.SetParent(head.transform);
            messy.transform.localPosition = new Vector3(0f, 0.15f, 0.2f);
            messy.transform.localScale = new Vector3(0.45f, 0.1f, 0.12f);
            SetMaterial(messy, Color.black);
            RemoveCollider(messy);
            for (int i = 0; i < 3; i++)
            {
                var strand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                strand.name = "HairStrand" + i; strand.transform.SetParent(head.transform);
                strand.transform.localPosition = new Vector3(-0.1f + i * 0.1f, -0.05f, 0.2f);
                strand.transform.localScale = new Vector3(0.015f, 0.15f, 0.015f);
                strand.transform.localEulerAngles = new Vector3(20f, 0f, 5f * (i - 1));
                SetMaterial(strand, Color.black);
                RemoveCollider(strand);
            }
        }

        static void AddGourd(GameObject root, Color color)
        {
            var lower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lower.name = "GourdLower"; lower.transform.SetParent(root.transform);
            lower.transform.localPosition = new Vector3(0.3f, 0.65f, 0f);
            lower.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            SetMaterial(lower, color);
            RemoveCollider(lower);
            var upper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            upper.name = "GourdUpper"; upper.transform.SetParent(root.transform);
            upper.transform.localPosition = new Vector3(0.3f, 0.78f, 0f);
            upper.transform.localScale = new Vector3(0.08f, 0.09f, 0.08f);
            SetMaterial(upper, color * 0.85f);
            RemoveCollider(upper);
            var cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cord.name = "GourdCord"; cord.transform.SetParent(root.transform);
            cord.transform.localPosition = new Vector3(0.2f, 0.72f, 0f);
            cord.transform.localEulerAngles = new Vector3(0f, 0f, 40f);
            cord.transform.localScale = new Vector3(0.01f, 0.08f, 0.01f);
            SetMaterial(cord, new Color(0.5f, 0.3f, 0.15f));
            RemoveCollider(cord);
        }

        // === CHILD COSTUME HELPERS ===
        static void AddMedicineBasket(GameObject root, Color color)
        {
            var basket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basket.name = "MedicineBasket"; basket.transform.SetParent(root.transform);
            basket.transform.localPosition = new Vector3(-0.3f, 0.55f, 0f);
            basket.transform.localScale = new Vector3(0.15f, 0.08f, 0.15f);
            SetMaterial(basket, color);
            RemoveCollider(basket);
            var herbs = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            herbs.name = "BasketHerbs"; herbs.transform.SetParent(basket.transform);
            herbs.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            herbs.transform.localScale = Vector3.one * 0.4f;
            SetMaterial(herbs, new Color(0.2f, 0.7f, 0.2f));
            RemoveCollider(herbs);
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "BasketHandle"; handle.transform.SetParent(root.transform);
            handle.transform.localPosition = new Vector3(-0.3f, 0.68f, 0f);
            handle.transform.localScale = new Vector3(0.01f, 0.08f, 0.01f);
            SetMaterial(handle, color * 0.7f);
            RemoveCollider(handle);
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
