﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ASP
{
    namespace IM
    {
        public class MonoDecal : MonoOverlay
        {
            static string _displayName = "Mono Decal";

            private string _url = string.Empty;
            private MonoDecalGui _gui;

            public MonoDecal()
            {
                _type = Type.MONO_DECAL;
            }

            public override void load(ConfigNode node)
            {
                _type = Type.MONO_DECAL;
                _url = string.Empty;

                loadMonoOverlay(node);

                if (node.HasValue("url")) _url = node.GetValue("url");
            }

            public override void save(ConfigNode node)
            {
                saveMonoOverlay(node);
                node.AddValue("url", _url);
            }

            public override void drawOnImage(ref Image image, BoundingBox boundingBox)
            {
                BitmapDecal decal;
                if (!BitmapDecalCache.Instance.decals.TryGetValue(_url, out decal)) return;

                Image decalImage = new Image(decal.image);
                Color32 color = new Color32(_red, _green, _blue, _alpha);
                decalImage.recolor(Global.Black32, color, false, true);
                decalImage.rotateImage(_rotation);

                image.blendImage(decalImage, _blendMethod, _position, _alphaOption, _textureAlpha, boundingBox);
            }

            public override void drawOnImage(ref Image image, ref Image normalMap, BoundingBox boundingBox)
            {
                drawDecalOnImage(ref image, ref normalMap, _url, boundingBox);
            }

            public override ImageModifier clone()
            {
                IM.MonoDecal im = new IM.MonoDecal();

                im._type = _type;
                im._url = _url;

                im.copyFromMonoOverlay(this);

                return im;
            }

            public override void cleanUp()
            {
                _gui = null;
            }

            public override string displayName()
            {
                return _displayName;
            }

            public override ImageModifierGui gui()
            {
                if (_gui == null) _gui = new MonoDecalGui(this);
                return _gui;
            }






            public class MonoDecalGui : MonoOverlayGui
            {
                private IM.MonoDecal _imMonoDecal;
                private int _selectedSheet = 0;
                private int _selectedDecal = 0;
                private Vector2 _scrollPos;
                private List<Texture2D> _textures = null;

                public MonoDecalGui(IM.MonoDecal monoDecal)
                    : base(monoDecal)
                {
                    _imMonoDecal = monoDecal;
                    _textures = null;
                }

                ~MonoDecalGui()
                {
                    if (_textures != null)
                    {
                        for (int i = 0; i < _textures.Count; ++i)
                            UnityEngine.Object.Destroy(_textures[i]);
                    }
                }

                public override void drawBottom(TextureEditGUI gui)
                {
                    GUILayout.BeginVertical(GUI.skin.box);

                    header(gui, "MONO DECAL");

                    GUILayout.Label("Url: " + _imMonoDecal._url, GUILayout.ExpandWidth(false));
                    GUILayout.Space(5);

                    drawBottomMonoOverlay(gui);

                    GUILayout.EndVertical();
                }

                public override void drawRight(TextureEditGUI gui)
                {
                    if (BitmapDecalCache.Instance.monoSheets.Count == 0) return;

                    Color contentColor = GUI.contentColor;
                    GUI.backgroundColor = Global.BackgroundColor;

                    _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUI.skin.box, GUILayout.MinWidth(250), GUILayout.ExpandHeight(true));

                    GUILayout.Label("Decal Sheets");

                    GUILayout.Space(3);

                    int oldSelectedSheet = _selectedSheet;
                    for (int i = 0; i < BitmapDecalCache.Instance.monoSheets.Count; ++i)
                    {
                        if (i == _selectedSheet) GUI.contentColor = Global.SelectedColor;
                        else GUI.contentColor = Global.NotSelectedColor;

                        if (GUILayout.Button(BitmapDecalCache.Instance.monoSheets[i].displayName, GUILayout.ExpandWidth(true))) _selectedSheet = i;
                    }

                    if (_selectedSheet != oldSelectedSheet)
                    {
                        if (_textures != null)
                        {
                            for (int i = 0; i < _textures.Count; ++i)
                            {
                                UnityEngine.Object.Destroy(_textures[i]);
                            }
                            _textures = null;
                        }
                        if (_selectedDecal >= BitmapDecalCache.Instance.monoSheets[_selectedSheet].decals.Count) _selectedDecal = 0;
                    }

                    GUILayout.Space(10);

                    GUILayout.Label("Decals");

                    GUILayout.Space(3);

                    if (_textures == null)
                    {
                        _textures = new List<Texture2D>();
                        for (int i = 0; i < BitmapDecalCache.Instance.monoSheets[_selectedSheet].decals.Count; ++i)
                        {
                            Image image = new Image(BitmapDecalCache.Instance.monoSheets[_selectedSheet].decals[i].image);
                            image.recolor(Global.Black32, Global.White32, false, false);

                            Texture2D texture = new Texture2D(image.width, image.height, TextureFormat.ARGB32, false);
                            texture.SetPixels32(image.pixels);
                            texture.Apply();

                            _textures.Add(texture);
                        }
                    }

                    int oldSelectedDecal = _selectedDecal;
                    int x = 0;
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < _textures.Count; ++i)
                    {
                        if (i == _selectedDecal)
                        {
                            GUI.backgroundColor = Color.yellow;
                            if (GUILayout.Button(_textures[i], GUILayout.Width(_textures[i].width + 4), GUILayout.Height(_textures[i].height + 4))) _selectedDecal = i;
                            GUI.backgroundColor = Global.BackgroundColor;
                        }
                        else
                        {
                            if (GUILayout.Button(_textures[i], GUILayout.Width(_textures[i].width + 4), GUILayout.Height(_textures[i].height + 4))) _selectedDecal = i;
                        }

                        x += _textures[i].width + 5;
                        if (i < (_textures.Count - 1))
                        {
                            if (x > 0 && (x + _textures[i+1].width) > 200)
                            {
                                GUILayout.EndHorizontal();
                                GUILayout.BeginHorizontal();
                                x = 0;
                            }
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndScrollView();

                    GUI.contentColor = contentColor;

                    if (oldSelectedSheet != _selectedSheet || oldSelectedDecal != _selectedDecal)
                    {
                        _imMonoDecal._url = BitmapDecalCache.Instance.monoSheets[_selectedSheet].decals[_selectedDecal].url;
                        gui.setRemakePreview();
                    }
                }

                public override void initialise(TextureEditGUI gui)
                {
                    initialiseMonoOverlay(gui);
                    _selectedSheet = 0;
                    _selectedDecal = 0;

                    if (BitmapDecalCache.Instance.monoSheets.Count > 0) _imMonoDecal._url = BitmapDecalCache.Instance.monoSheets[_selectedSheet].decals[_selectedDecal].url;
                }

                public override string buttonText()
                {
                    if (_imMonoDecal._url == string.Empty) return "Mono Decal";
                    else if (_imMonoDecal._url.Length < 8) return _imMonoDecal._url;
                    else return ".." + _imMonoDecal._url.Substring(_imMonoDecal._url.Length - 7, 7);
                }
            }
        }
    }
}