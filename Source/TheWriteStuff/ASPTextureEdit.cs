﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ASP
{
    public class ASPTextureEdit : PartModule
    {
        [KSPField(isPersistant = true)]
        public string transformNames = string.Empty;

        [KSPField(isPersistant = true)]
        public string baseTextureName = string.Empty;

        [KSPField(isPersistant = true)]
        public string baseNormalMapName = string.Empty;

        [KSPField(isPersistant = true)]
        public string baseTextureDirUrl = string.Empty;

        [KSPField(isPersistant = true)]
        public bool outputReadable = false;

        // originialConfig is serialized (copied) when the part is duplicated in the editor
        [SerializeField]
        public ConfigNode originalConfig;

        public KSPTextureInfo kspTextureInfo { get; private set; }

        private TransformOption _transformsOption = TransformOption.USE_FIRST;
        private BoundingBox _boundingBox = null;
        private IM.BaseTexture _baseTexture = null;
        private ImageModifiers _imageModifiers = null;

        private bool _ok = false;
        private List<string> _transformNames = null;
        private List<Transform> _transforms = null;
        private TextureEditGUI _gui;
        private Texture2D _generatedMainTexture = null;
        private Texture2D _generatedNormalMap = null;

        ~ASPTextureEdit()
        {
            if (_generatedMainTexture != null) Destroy(_generatedMainTexture);
            if (_generatedNormalMap != null) Destroy(_generatedNormalMap);
        }

        [KSPEvent(name = "Edit Texture Event", guiName = "Edit Texture", guiActive = false, guiActiveEditor = true)]
        public void editTextureEvent()
        {
            if (_ok == false)
            {
                // something has gone wrong in OnStart
                Utils.LogError("Incorrect start up, not displaying gui");
                ScreenMessages.PostScreenMessage("Error unable to start text writer gui", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            _gui = gameObject.GetComponent<TextureEditGUI>();
            if (_gui == null)
            {
                _gui = gameObject.AddComponent<TextureEditGUI>();
                _gui.initialise(this);
            }
        }

        public ImageModifiers cloneImageModifiers()
        {
            return _imageModifiers.clone();
        }

        public IM.BaseTexture cloneBaseTexture()
        {
            return _baseTexture.cloneBaseTexture();
        }

        public BoundingBox cloneBoundingBox()
        {
            return _boundingBox.clone();
        }

        public void setBaseTexture(IM.BaseTexture baseTexture)
        {
            _baseTexture = baseTexture.cloneBaseTexture();
        }

        public void setImageModifiers(ImageModifiers imageModifiers)
        {
            _imageModifiers = imageModifiers.clone();
        }

        public void setBoundingBox(BoundingBox boundingBox)
        {
            _boundingBox = boundingBox.clone();
        }

        public void writeTexture()
        {
            if (Global.Debug2) Utils.Log("writeTexture start");

            Image textureImage = new Image();
            Image normalMapImage = new Image();

            if (_baseTexture.hasNormalMap())
            {
                _baseTexture.drawOnImage(ref textureImage, ref normalMapImage);
                _imageModifiers.drawOnImage(ref textureImage, ref normalMapImage, _boundingBox);
            }
            else
            {
                _baseTexture.drawOnImage(ref textureImage);
                _imageModifiers.drawOnImage(ref textureImage, _boundingBox);
            }

            Texture2D mainTexture = new Texture2D(textureImage.width, textureImage.height, TextureFormat.ARGB32, true);
            mainTexture.SetPixels32(textureImage.pixels);

            if (outputReadable) mainTexture.Apply(true);
            else
            {
                mainTexture.Apply(true);
                mainTexture.Compress(true);
                mainTexture.Apply(false, true);
            }

            foreach (Transform transform in _transforms)
            {
                if (Global.Debug3) Utils.Log("setting texture in transform {0}", transform.name);
                transform.gameObject.renderer.material.SetTexture("_MainTex", mainTexture);
            }

            Texture2D normalMapTexture = null;
            if (_baseTexture.hasNormalMap())
            {
                normalMapTexture = new Texture2D(normalMapImage.width, normalMapImage.height, TextureFormat.ARGB32, false);
                normalMapTexture.SetPixels32(normalMapImage.pixels);

                if (outputReadable) normalMapTexture.Apply(false);
                else normalMapTexture.Apply(false, true);

                foreach (Transform transform in _transforms)
                {
                    if (Global.Debug3) Utils.Log("setting normalMap in transform {0}", transform.name);
                    transform.gameObject.renderer.material.SetTexture("_BumpMap", normalMapTexture);
                }
            }

            if (_generatedMainTexture != null) Destroy(_generatedMainTexture);
            _generatedMainTexture = mainTexture;

            if (_generatedNormalMap != null) Destroy(_generatedNormalMap);
            if (normalMapTexture != null) _generatedNormalMap = normalMapTexture;
            else _generatedNormalMap = null;

            // clean up memory usage
            _baseTexture.cleanUp();
            _imageModifiers.cleanUp();
        }

        private string findTransformName(Transform[] transforms, bool validTexture, bool validNormalMap)
        {
            foreach (Transform child in transforms)
            {
                if (child.gameObject.renderer != null && child.gameObject.renderer.material != null)
                {
                    if (validTexture == false) return child.name;

                    Texture2D main = child.gameObject.renderer.material.mainTexture as Texture2D;

                    if (main != null && main.name != string.Empty)
                    {
                        if (validNormalMap)
                        {
                            Texture2D normal = child.gameObject.renderer.material.GetTexture("_BumpMap") as Texture2D;

                            if (normal != null && normal.name != string.Empty) return child.name;
                        }
                        else return child.name;
                    }
                }
            }

            return string.Empty;
        }

        private string findFirstUseableTransform()
        {
            string transformName = string.Empty;
            Transform[] children = this.part.partInfo.partPrefab.GetComponentsInChildren<Transform>(true);

            // use the first transform with both a texture and a normal map with non empty names
            transformName = findTransformName(children, true, true);
            if (Global.Debug2) Utils.Log("findTransformName,true,true = {0}", transformName);
            if (transformName != string.Empty) return transformName;

            // if not use the first tranform with a texture and non empty name
            transformName = findTransformName(children, true, false);
            if (Global.Debug2) Utils.Log("findTransformName,true,false = {0}", transformName);
            if (transformName != string.Empty) return transformName;

            // fall back to the first object with a material
            transformName = findTransformName(children, false, false);
            if (Global.Debug2) Utils.Log("findTransformName,false,false = {0}", transformName);
            return transformName;
        }

        private void fillTransformNamesList()
        {
            if (transformNames == string.Empty) transformNames = findFirstUseableTransform();
            if (transformNames == string.Empty)
            {
                Utils.LogError("unable to find transform with material, part {0}.", this.part.name);
                throw new ArgumentException("unable to find transform");
            }

            _transformNames = new List<string>(Utils.SplitString(transformNames));
            if (_transformNames == null || _transformNames.Count == 0 || _transformNames[0] == string.Empty)
            {
                Utils.LogError("transformNames empty, part {0}.", this.part.name);
                throw new ArgumentException("transformNames empty");
            }
        }

        private void fillTransformsList()
        {
            int count = 0;
            _transforms = new List<Transform>();

            foreach (string transformName in _transformNames)
            {
                Transform[] transforms = this.part.FindModelTransforms(transformName);

                if (transforms != null && transforms.Length > 0)
                {
                    int c = 0;
                    for (int i = 0; i < transforms.Length; ++i)
                    {
                        if (transforms[i] != null)
                        {
                            _transforms.Add(transforms[i]);
                            ++count;
                            ++c;

                            if (_transformsOption == TransformOption.USE_FIRST) break;
                        }
                    }
                    if (Global.Debug1) Utils.Log("found transform {0}, {1} times", transformName, c);
                }
                else Utils.LogError("unable to find transform {0}", transformName);
            }
            if (Global.Debug1) Utils.Log("found {0} usable transforms", count);
        }

        private void findTextures()
        {
            if (Global.Debug2)
            {
                Utils.Log("baseTextureName: [{0}]", baseTextureName);
                Utils.Log("baseNormalMapName: [{0}]", baseNormalMapName);
            }

            if (baseTextureName == string.Empty) kspTextureInfo = new KSPTextureInfo(_transforms[0]);
            else kspTextureInfo = new KSPTextureInfo(baseTextureDirUrl, baseTextureName, baseNormalMapName, _transforms[0]);

            if (Global.Debug1)
            {
                Utils.Log("_baseTextureInfo.mainUrl: {0}", kspTextureInfo.mainUrl);
                Utils.Log("_baseTextureInfo.normalMapUrl: {0}", kspTextureInfo.normalMapUrl);
                Utils.Log("_baseTextureInfo.displayName: {0}", kspTextureInfo.displayName);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            if (Global.Debug2) Utils.Log("OnSave start");

            if (_boundingBox != null)
            {
                ConfigNode bbNode = new ConfigNode("ASP_BOUNDINGBOX");
                _boundingBox.save(bbNode);
                node.AddNode(bbNode);
            }

            node.AddValue("transformsOption", ConfigNode.WriteEnum(_transformsOption));

            if (_baseTexture != null)
            {
                ConfigNode imNode = new ConfigNode("ASP_BASETEXTURE");
                _baseTexture.save(imNode);
                node.AddNode(imNode);
            }

            if (_imageModifiers != null)
            {
                ConfigNode imNode = new ConfigNode("ASP_IMAGEMODIFIERS");
                _imageModifiers.save(imNode);
                node.AddNode(imNode);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            if (Global.Debug2) Utils.Log("OnLoad start");

            // save the original config
            originalConfig = new ConfigNode();
            node.CopyTo(originalConfig);

            loadConfig(node);
        }

        private void loadConfig(ConfigNode node)
        {
            if (Global.Debug3) Utils.Log("node {0}", node.ToString());

            _ok = false;
            _transformNames = null;
            _transforms = null;
            kspTextureInfo = null;
            if (_gui != null) Destroy(_gui);
            _gui = null;
            _boundingBox = null;
            _transformsOption = TransformOption.USE_ALL;
            if (_baseTexture != null) _baseTexture.cleanUp();
            _baseTexture = null;
            if (_imageModifiers != null) _imageModifiers.cleanUp();
            _imageModifiers = null;

            if (node.HasNode("ASP_BOUNDINGBOX"))
            {
                if (_boundingBox == null) _boundingBox = new BoundingBox();
                ConfigNode n = node.GetNode("ASP_BOUNDINGBOX");
                _boundingBox.load(n);
            }

            if (node.HasValue("transformsOption")) _transformsOption = (TransformOption)ConfigNode.ParseEnum(typeof(TransformOption), node.GetValue("transformsOption"));

            if (node.HasValue("ASP_BASETEXTURE"))
            {
                _baseTexture = new IM.BaseTexture();
                ConfigNode n = node.GetNode("ASP_BASETEXTURE");
                _baseTexture.load(n);
            }

            if (node.HasNode("ASP_IMAGEMODIFIERS"))
            {
                _imageModifiers = new ImageModifiers();
                ConfigNode n = node.GetNode("ASP_IMAGEMODIFIERS");
                _imageModifiers.load(n);
            }
        }

        public override void OnStart(StartState state)
        {
            if (Global.Debug2) Utils.Log("state {0}", state.ToString());
            base.OnStart(state);

            // side step serialization issues and use the saved config
            if (state == StartState.Editor)
            {
                if (originalConfig == null)
                {
                    if (Global.Debug1) Utils.Log("originalConfig is null");
                }
                else loadConfig(originalConfig);
            }

            _ok = false;
            try
            {
                if (Global.Debug1) Utils.Log("part {0}", this.part.name);

                fillTransformNamesList();
                fillTransformsList();

                if (_transforms == null || _transforms[0] == null)
                {
                    Utils.LogError("No useable transforms, disabling plugin");
                    return;
                }

                findTextures();

                if (_imageModifiers == null) _imageModifiers = new ImageModifiers();
                if (_boundingBox == null) _boundingBox = new BoundingBox();
                if (_baseTexture == null) _baseTexture = new IM.BaseTexture();

                _baseTexture.set(kspTextureInfo);

                if (_imageModifiers.modifiers.Count > 0) writeTexture();

                _ok = true;
            }
            catch
            {
                Utils.LogError("Something went wrong in OnStart disabling plugin");
            }
        }
    }
}