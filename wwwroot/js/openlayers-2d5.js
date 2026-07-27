(function () {
    // Intercept ol.Map constructor to capture map instance automatically
    if (window.ol && window.ol.Map && !window.olMapIntercepted) {
        window.olMapIntercepted = true;
        const OriginalMap = window.ol.Map;
        window.ol.Map = function (options) {
            const map = new OriginalMap(options);
            window.activeOlMap = map;
            console.log("[2.5D Log] Intercepted ol.Map instance successfully! Target element ID:", options.target);
            if (window._pendingLayersToRender && window._pendingLayersToRender.length > 0) {
                const pending = window._pendingLayersToRender.slice();
                window._pendingLayersToRender = [];
                pending.forEach(fn => fn());
            }
            return map;
        };
        window.ol.Map.prototype = OriginalMap.prototype;
    }
})();

window._mapLayers = window._mapLayers || {};
window._desiredZIndices = window._desiredZIndices || {};
window._desiredVisibilities = window._desiredVisibilities || {};
window._pendingLayersToRender = window._pendingLayersToRender || [];
window._draftVectorLayers = window._draftVectorLayers || {};
window._currentDraftLayerId = null;

// Static Style Cache (Zero allocation overhead)
const STATIC_STYLES = {
    shadow: new ol.style.Style({
        fill: new ol.style.Fill({ color: 'rgba(15, 23, 42, 0.12)' })
    }),
    wallLow: new ol.style.Style({
        fill: new ol.style.Fill({ color: 'rgba(203, 213, 225, 0.60)' }),
        stroke: new ol.style.Stroke({ color: 'rgba(148, 163, 184, 0.50)', width: 0.8 })
    }),
    roofLow: new ol.style.Style({
        fill: new ol.style.Fill({ color: 'rgba(226, 232, 240, 0.70)' }),
        stroke: new ol.style.Stroke({ color: 'rgba(148, 163, 184, 0.60)', width: 1.0 })
    }),
    wallStd: new ol.style.Style({
        fill: new ol.style.Fill({ color: 'rgba(148, 163, 184, 0.85)' }),
        stroke: new ol.style.Stroke({ color: 'rgba(71, 85, 105, 0.45)', width: 0.8 })
    }),
    roofStd: new ol.style.Style({
        fill: new ol.style.Fill({ color: 'rgba(238, 242, 252, 0.94)' }),
        stroke: new ol.style.Stroke({ color: 'rgba(100, 116, 139, 0.65)', width: 1.0 })
    }),
    wallHigh: new ol.style.Style({
        fill: new ol.style.Fill({ color: 'rgba(100, 116, 139, 0.92)' }),
        stroke: new ol.style.Stroke({ color: 'rgba(51, 65, 85, 0.80)', width: 1.0 })
    }),
    roofHigh: new ol.style.Style({
        fill: new ol.style.Fill({ color: 'rgba(255, 255, 255, 0.98)' }),
        stroke: new ol.style.Stroke({ color: 'rgba(71, 85, 105, 0.85)', width: 1.2 })
    })
};

window.uspsim2d5 = {
    configureMap: function (mapOL) {
        console.log("[2.5D Log] ConfigureJsMethod invoked with mapOL object:", mapOL);
        if (mapOL) {
            window.activeOlMap = mapOL.Map ? mapOL.Map : mapOL;
            console.log("[2.5D Log] Captured OpenLayers map instance successfully:", window.activeOlMap);

            if (window._pendingLayersToRender && window._pendingLayersToRender.length > 0) {
                const pending = window._pendingLayersToRender.slice();
                window._pendingLayersToRender = [];
                pending.forEach(fn => fn());
            }
        }
    },

    loadSessionLayer: function (sessionId, layerKey) {
        console.log(`[Map Control] Fetching layer '${layerKey}' for Session #${sessionId} via HTTP API...`);
        const apiUrl = `/api/layers/${sessionId}/${encodeURIComponent(layerKey)}`;

        fetch(apiUrl)
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status} when fetching layer '${layerKey}'`);
                }
                return response.json();
            })
            .then(geoJsonObj => {
                console.log(`[Map Control] Successfully fetched GeoJSON payload for '${layerKey}' via HTTP API! Features count: ${geoJsonObj.features ? geoJsonObj.features.length : 0}`);
                if (layerKey === 'pdok-3dbag-buildings') {
                    window.uspsim2d5.renderBuildings(geoJsonObj);
                } else {
                    window.uspsim2d5.renderInfrastructureLayer(geoJsonObj, layerKey);
                }
            })
            .catch(err => {
                console.error(`[Map Control] Error loading layer '${layerKey}':`, err);
            });
    },

    setLayerVisibility: function (layerKey, isVisible) {
        window._desiredVisibilities[layerKey] = isVisible;

        const layer = window._mapLayers ? window._mapLayers[layerKey] : null;
        if (layer) {
            layer.setVisible(isVisible);
            if (window.activeOlMap) window.activeOlMap.render();
        } else {
            console.log(`[Map Control] Stored desired visibility '${isVisible}' for pending layer '${layerKey}'.`);
        }
    },

    setLayerZIndex: function (layerKey, zIndex) {
        window._desiredZIndices[layerKey] = zIndex;

        const layer = window._mapLayers ? window._mapLayers[layerKey] : null;
        if (layer) {
            layer.setZIndex(zIndex);
            if (window.activeOlMap) window.activeOlMap.render();
        } else {
            console.log(`[Map Control] Stored desired zIndex '${zIndex}' for pending layer '${layerKey}'.`);
        }
    },

    renderBuildings: function (geoJsonInput) {
        if (!geoJsonInput || !window.ol) {
            console.warn("[2.5D Log] Aborting: Missing GeoJSON data or OpenLayers window.ol is undefined.");
            return;
        }

        try {
            const startTime = performance.now();
            const geoJsonObj = typeof geoJsonInput === 'string' ? JSON.parse(geoJsonInput) : geoJsonInput;
            const format = new ol.format.GeoJSON();
            const rawFeatures = format.readFeatures(geoJsonObj, { featureProjection: 'EPSG:3857' });

            if (!rawFeatures || rawFeatures.length === 0) {
                console.warn("[2.5D Log] Aborting: 0 features found in GeoJSON.");
                return;
            }

            console.log(`[2.5D Log] OPTION A: Grouping ${rawFeatures.length} building footprints into 5 global MultiPolygon features...`);

            const buildingMetadataList = [];

            rawFeatures.forEach(feature => {
                const geom = feature.getGeometry();
                if (!geom) return;

                const geomType = geom.getType();
                let polygonRingsList = [];
                if (geomType === 'Polygon') {
                    polygonRingsList.push(geom.getCoordinates());
                } else if (geomType === 'MultiPolygon') {
                    polygonRingsList = geom.getCoordinates();
                } else {
                    return;
                }

                const props = feature.getProperties() || {};
                const hRoof = props.b3_h_70p ?? props.b3_h_max ?? props.b3_h_50p ?? 0;
                const hGround = props.b3_h_maaiveld ?? 0;

                let heightMeters = Math.max(hRoof - hGround, 1.5);
                if (props.b3_is_glas_dak || props.status === 'Pand buiten gebruik') {
                    heightMeters = Math.min(heightMeters, 5.0);
                }

                const heightPixels = Math.min(Math.max(heightMeters * 0.48, 3), 32);

                buildingMetadataList.push({
                    heightPixels: heightPixels,
                    heightMeters: heightMeters,
                    ringsList: polygonRingsList
                });
            });

            // 5 Consolidated Features for 99.9% R-Tree Spatial Index Reduction
            const shadowFeature = new ol.Feature();
            const wallLowFeature = new ol.Feature();
            const roofLowFeature = new ol.Feature();
            const wallStdFeature = new ol.Feature();
            const roofStdFeature = new ol.Feature();
            const wallHighFeature = new ol.Feature();
            const roofHighFeature = new ol.Feature();

            shadowFeature.setStyle(STATIC_STYLES.shadow);
            wallLowFeature.setStyle(STATIC_STYLES.wallLow);
            roofLowFeature.setStyle(STATIC_STYLES.roofLow);
            wallStdFeature.setStyle(STATIC_STYLES.wallStd);
            roofStdFeature.setStyle(STATIC_STYLES.roofStd);
            wallHighFeature.setStyle(STATIC_STYLES.wallHigh);
            roofHighFeature.setStyle(STATIC_STYLES.roofHigh);

            let lastResolution = -1;

            const updateExtrusionGeometries = function (resolution) {
                if (resolution === lastResolution) return;
                lastResolution = resolution;

                const shadowRingsList = [];
                const wallLowRingsList = [];
                const roofLowRingsList = [];
                const wallStdRingsList = [];
                const roofStdRingsList = [];
                const wallHighRingsList = [];
                const roofHighRingsList = [];

                buildingMetadataList.forEach(item => {
                    const dx = item.heightPixels * resolution * 0.45;
                    const dy = item.heightPixels * resolution * 0.75;

                    item.ringsList.forEach(rings => {
                        if (!rings || rings.length === 0) return;
                        shadowRingsList.push(rings);

                        const targetWallList = item.heightMeters <= 4 ? wallLowRingsList : (item.heightMeters >= 35 ? wallHighRingsList : wallStdRingsList);
                        const targetRoofList = item.heightMeters <= 4 ? roofLowRingsList : (item.heightMeters >= 35 ? roofHighRingsList : roofStdRingsList);

                        rings.forEach(ring => {
                            if (!ring || ring.length < 3) return;

                            for (let i = 0; i < ring.length - 1; i++) {
                                const p1 = ring[i];
                                const p2 = ring[i + 1];

                                const wallPoly = [
                                    p1,
                                    p2,
                                    [p2[0] + dx, p2[1] + dy],
                                    [p1[0] + dx, p1[1] + dy],
                                    p1
                                ];
                                targetWallList.push([wallPoly]);
                            }
                        });

                        const offsetRings = rings.map(ring => ring.map(pt => [pt[0] + dx, pt[1] + dy]));
                        targetRoofList.push(offsetRings);
                    });
                });

                if (shadowRingsList.length > 0) shadowFeature.setGeometry(new ol.geom.MultiPolygon(shadowRingsList));
                if (wallLowRingsList.length > 0) wallLowFeature.setGeometry(new ol.geom.MultiPolygon(wallLowRingsList));
                if (roofLowRingsList.length > 0) roofLowFeature.setGeometry(new ol.geom.MultiPolygon(roofLowRingsList));
                if (wallStdRingsList.length > 0) wallStdFeature.setGeometry(new ol.geom.MultiPolygon(wallStdRingsList));
                if (roofStdRingsList.length > 0) roofStdFeature.setGeometry(new ol.geom.MultiPolygon(roofStdRingsList));
                if (wallHighRingsList.length > 0) wallHighFeature.setGeometry(new ol.geom.MultiPolygon(wallHighRingsList));
                if (roofHighRingsList.length > 0) roofHighFeature.setGeometry(new ol.geom.MultiPolygon(roofHighRingsList));
            };

            const consolidatedFeatures = [
                shadowFeature,
                wallLowFeature, roofLowFeature,
                wallStdFeature, roofStdFeature,
                wallHighFeature, roofHighFeature
            ];

            const initialZIndex = window._desiredZIndices["pdok-3dbag-buildings"] ?? 100;
            const initialVisible = window._desiredVisibilities["pdok-3dbag-buildings"] ?? true;

            const vectorSource = new ol.source.Vector({ features: consolidatedFeatures });
            const vectorLayer = new ol.layer.Vector({
                source: vectorSource,
                zIndex: initialZIndex,
                visible: initialVisible
            });

            window._mapLayers["pdok-3dbag-buildings"] = vectorLayer;

            const tryAddToMap = function () {
                let mapInstance = window.activeOlMap;

                if (mapInstance) {
                    if (window._current2D5Layer) {
                        try { mapInstance.removeLayer(window._current2D5Layer); } catch (e) { }
                    }

                    // Dynamic Resolution Sync Listener for 60 FPS Camera Scaling
                    mapInstance.getView().on('change:resolution', function () {
                        updateExtrusionGeometries(mapInstance.getView().getResolution());
                    });

                    // Initial Geometry Sync
                    updateExtrusionGeometries(mapInstance.getView().getResolution());

                    mapInstance.addLayer(vectorLayer);
                    window._current2D5Layer = vectorLayer;
                    mapInstance.render();

                    const elapsedMs = Math.round(performance.now() - startTime);
                    console.log(`[2.5D Log] OPTION A SUCCESS: Rendered ${rawFeatures.length} buildings in ${elapsedMs}ms! Merged into 7 global MultiPolygons.`);
                    return true;
                } else {
                    console.warn("[2.5D Log] Map instance not bound yet, queueing building layer render...");
                    window._pendingLayersToRender.push(tryAddToMap);
                    return false;
                }
            };

            tryAddToMap();

        } catch (err) {
            console.error("[2.5D Log] Fatal error rendering building layer:", err);
        }
    },

    renderInfrastructureLayer: function (geoJsonInput, targetKey) {
        const layerKey = targetKey || "liander-open-data-elektra";
        console.log(`[Infrastructure Log] renderInfrastructureLayer invoked for '${layerKey}'.`);

        if (!geoJsonInput || !window.ol) return;

        try {
            const geoJsonObj = typeof geoJsonInput === 'string' ? JSON.parse(geoJsonInput) : geoJsonInput;
            const format = new ol.format.GeoJSON();
            const features = format.readFeatures(geoJsonObj, { featureProjection: 'EPSG:3857' });

            if (!features || features.length === 0) {
                console.warn(`[Infrastructure Log] 0 features found in electrical grid payload for '${layerKey}'.`);
                return;
            }

            const infraStyleFunction = function (feature) {
                const props = feature.getProperties() || {};
                const id = (feature.getId() || "").toLowerCase();
                const gmlId = (props.GmlID || "").toLowerCase();
                const geomType = feature.getGeometry() ? feature.getGeometry().getType() : '';
                const isSewage = layerKey.includes('sewage') || layerKey.includes('gwsw') || id.includes('beheer_');

                if (geomType === 'Point' || geomType === 'MultiPoint') {
                    const isStation = layerKey.includes('station') || id.includes('station') || gmlId.includes('station');
                    const ptColor = isSewage ? '#059669' : (isStation ? '#ef4444' : '#0284c7');
                    return new ol.style.Style({
                        image: new ol.style.Circle({
                            radius: isStation ? 7 : (isSewage ? 4 : 5),
                            fill: new ol.style.Fill({ color: ptColor }),
                            stroke: new ol.style.Stroke({ color: '#ffffff', width: 1.5 })
                        })
                    });
                }

                if (geomType === 'Polygon' || geomType === 'MultiPolygon') {
                    if (layerKey.includes('bestuurlijkegebieden') || layerKey.includes('gemeente') || id.includes('gemeentegebied')) {
                        return new ol.style.Style({
                            fill: new ol.style.Fill({ color: 'rgba(99, 102, 241, 0.08)' }),
                            stroke: new ol.style.Stroke({ color: '#4f46e5', width: 2.5, lineDash: [8, 4] })
                        });
                    }
                    if (layerKey.includes('kadastralekaart') || id.includes('perceel')) {
                        return new ol.style.Style({
                            fill: new ol.style.Fill({ color: 'rgba(100, 116, 139, 0.08)' }),
                            stroke: new ol.style.Stroke({ color: '#475569', width: 1.5 })
                        });
                    }
                    const fillColor = isSewage ? 'rgba(5, 150, 105, 0.30)' : 'rgba(239, 68, 68, 0.35)';
                    const strokeColor = isSewage ? '#047857' : '#dc2626';
                    return new ol.style.Style({
                        fill: new ol.style.Fill({ color: fillColor }),
                        stroke: new ol.style.Stroke({ color: strokeColor, width: 2 })
                    });
                }

                let strokeColor = '#0284c7';
                let strokeWidth = 2.5;

                if (isSewage) {
                    strokeColor = '#059669'; // Emerald green for urban sewage/drainage
                    strokeWidth = 3.0;
                } else if (layerKey.includes('hoogspanning') || gmlId.includes('hoogspanning') || id.includes('hoogspanning') || id.includes('stedin_hs')) {
                    strokeColor = '#f59e0b';
                    strokeWidth = 4.5;
                } else if (layerKey.includes('station') || gmlId.includes('station') || id.includes('station') || id.includes('stedin_mls')) {
                    strokeColor = '#ef4444';
                    strokeWidth = 3.5;
                } else if (gmlId.includes('middenspanning') || id.includes('middenspanning')) {
                    strokeColor = '#a855f7';
                    strokeWidth = 3.5;
                }

                return new ol.style.Style({
                    stroke: new ol.style.Stroke({
                        color: strokeColor,
                        width: strokeWidth
                    })
                });
            };

            const initialZIndex = window._desiredZIndices[layerKey] ?? 110;
            const initialVisible = window._desiredVisibilities[layerKey] ?? true;

            const vectorSource = new ol.source.Vector({ features: features });
            const vectorLayer = new ol.layer.Vector({
                source: vectorSource,
                style: infraStyleFunction,
                zIndex: initialZIndex,
                visible: initialVisible
            });

            window._mapLayers[layerKey] = vectorLayer;

            const tryAddToMap = function () {
                let mapInstance = window.activeOlMap;

                if (mapInstance) {
                    if (window._currentInfraLayers && window._currentInfraLayers[layerKey]) {
                        try { mapInstance.removeLayer(window._currentInfraLayers[layerKey]); } catch (e) { }
                    }
                    window._currentInfraLayers = window._currentInfraLayers || {};
                    mapInstance.addLayer(vectorLayer);
                    window._currentInfraLayers[layerKey] = vectorLayer;
                    mapInstance.render();
                    console.log(`[Infrastructure Log] SUCCESS: Added Electrical Network layer '${layerKey}' (${features.length} features, zIndex=${initialZIndex}, visible=${initialVisible}).`);
                    return true;
                } else {
                    console.warn(`[Infrastructure Log] Map instance not bound yet, queueing infrastructure layer '${layerKey}' render...`);
                    window._pendingLayersToRender.push(tryAddToMap);
                    return false;
                }
            };

            tryAddToMap();

        } catch (err) {
            console.error(`[Infrastructure Log] Error rendering infrastructure layer '${layerKey}':`, err);
        }
    },

    startDrawing: function (geomType, strokeColor, fillColor, layerId) {
        const self = this;
        const map = window.activeOlMap;
        if (!map) {
            console.warn('[uspsim2d5] Map instance not ready for drawing yet, retrying in 100ms...');
            setTimeout(function () {
                self.startDrawing(geomType, strokeColor, fillColor, layerId);
            }, 100);
            return;
        }

        this.stopInteractionOnly();

        layerId = layerId || 'default';
        window._currentDraftLayerId = layerId;

        if (!window._draftVectorLayers[layerId]) {
            const source = new ol.source.Vector();
            const strokeCol = strokeColor || '#3b82f6';
            const fillCol = fillColor || 'rgba(59, 130, 246, 0.25)';

            const layer = new ol.layer.Vector({
                source: source,
                style: function (feature) {
                    const mainStyle = new ol.style.Style({
                        fill: new ol.style.Fill({ color: fillCol }),
                        stroke: new ol.style.Stroke({ color: strokeCol, width: 3 }),
                        image: new ol.style.Circle({
                            radius: 7,
                            fill: new ol.style.Fill({ color: strokeCol }),
                            stroke: new ol.style.Stroke({ color: '#ffffff', width: 2 })
                        })
                    });

                    const styles = [mainStyle];
                    const geom = feature.getGeometry();
                    if (geom) {
                        let coords = [];
                        const type = geom.getType();
                        if (type === 'Polygon') {
                            const rings = geom.getCoordinates();
                            if (rings && rings[0]) {
                                coords = rings[0].slice(0, rings[0].length - 1);
                            }
                        } else if (type === 'LineString') {
                            coords = geom.getCoordinates();
                        }

                        if (coords && coords.length > 0) {
                            const vertexGeom = new ol.geom.MultiPoint(coords);
                            const vertexStyle = new ol.style.Style({
                                geometry: vertexGeom,
                                image: new ol.style.Circle({
                                    radius: 5,
                                    fill: new ol.style.Fill({ color: '#ffffff' }),
                                    stroke: new ol.style.Stroke({ color: strokeCol, width: 2.5 })
                                })
                            });
                            styles.push(vertexStyle);
                        }
                    }

                    const sel = window._selectedDraftVertex;
                    if (sel && sel.feature === feature) {
                        let selCoord = sel.coordinate;
                        if (!selCoord && geom) {
                            if (geom.getType() === 'Point') {
                                selCoord = geom.getCoordinates();
                            }
                        }

                        if (selCoord) {
                            const selGeom = new ol.geom.Point(selCoord);
                            const selStyle = new ol.style.Style({
                                geometry: selGeom,
                                image: new ol.style.Circle({
                                    radius: 10,
                                    fill: new ol.style.Fill({ color: 'rgba(245, 158, 11, 0.45)' }),
                                    stroke: new ol.style.Stroke({ color: '#f59e0b', width: 3 })
                                })
                            });
                            styles.push(selStyle);
                        }
                    }

                    return styles;
                },
                zIndex: 998
            });
            map.addLayer(layer);
            window._draftVectorLayers[layerId] = {
                source: source,
                layer: layer,
                strokeColor: strokeColor,
                fillColor: fillColor,
                undoStack: [],
                redoStack: []
            };
        }

        const currentDraft = window._draftVectorLayers[layerId];
        window._drawSource = currentDraft.source;

        let olGeomType = 'Polygon';
        if (geomType === 'Line' || geomType === 'LineString') olGeomType = 'LineString';
        else if (geomType === 'Point') olGeomType = 'Point';
        else if (geomType === 'Polygon') olGeomType = 'Polygon';

        window._drawInteraction = new ol.interaction.Draw({
            source: window._drawSource,
            type: olGeomType
        });

        window._drawRedoStack = [];
        window._activeSketchGeometry = null;

        window._drawInteraction.on('drawstart', function (evt) {
            window._drawRedoStack = [];
            window._activeSketchGeometry = evt.feature.getGeometry();
        });

        window._drawInteraction.on('drawend', function (evt) {
            if (evt.feature && currentDraft) {
                currentDraft.undoStack.push(evt.feature);
                currentDraft.redoStack = [];
            }
            window._activeSketchGeometry = null;
        });

        window._modifyInteraction = new ol.interaction.Modify({
            source: window._drawSource
        });

        map.addInteraction(window._drawInteraction);
        map.addInteraction(window._modifyInteraction);

        if (!window._draftMapClickListener) {
            window._draftMapClickListener = function (evt) {
                const activeLayerId = window._currentDraftLayerId;
                if (!activeLayerId || !window._draftVectorLayers || !window._draftVectorLayers[activeLayerId]) return;
                const draft = window._draftVectorLayers[activeLayerId];
                const activeMap = window.activeOlMap;
                if (!activeMap) return;

                const clickCoord = evt.coordinate;
                let foundSelection = null;
                const toleranceMeters = activeMap.getView().getResolution() * 14;

                const features = draft.source.getFeatures();
                for (let fIdx = 0; fIdx < features.length; fIdx++) {
                    const feat = features[fIdx];
                    const geom = feat.getGeometry();
                    if (!geom) continue;
                    const type = geom.getType();

                    if (type === 'Point') {
                        const pCoord = geom.getCoordinates();
                        const dist = Math.hypot(pCoord[0] - clickCoord[0], pCoord[1] - clickCoord[1]);
                        if (dist <= toleranceMeters) {
                            foundSelection = {
                                feature: feat,
                                geomType: 'Point',
                                vertexIndex: -1,
                                coordinate: pCoord
                            };
                            break;
                        }
                    } else if (type === 'LineString') {
                        const coords = geom.getCoordinates();
                        for (let i = 0; i < coords.length; i++) {
                            const c = coords[i];
                            const dist = Math.hypot(c[0] - clickCoord[0], c[1] - clickCoord[1]);
                            if (dist <= toleranceMeters) {
                                foundSelection = {
                                    feature: feat,
                                    geomType: 'LineString',
                                    vertexIndex: i,
                                    coordinate: c
                                };
                                break;
                            }
                        }
                    } else if (type === 'Polygon') {
                        const rings = geom.getCoordinates();
                        if (rings && rings[0]) {
                            const ring = rings[0];
                            const distinctLen = ring.length - 1;
                            for (let i = 0; i < distinctLen; i++) {
                                const c = ring[i];
                                const dist = Math.hypot(c[0] - clickCoord[0], c[1] - clickCoord[1]);
                                if (dist <= toleranceMeters) {
                                    foundSelection = {
                                        feature: feat,
                                        geomType: 'Polygon',
                                        vertexIndex: i,
                                        coordinate: c
                                    };
                                    break;
                                }
                            }
                        }
                    }
                    if (foundSelection) break;
                }

                window._selectedDraftVertex = foundSelection;
                draft.source.changed();
            };

            map.on('singleclick', window._draftMapClickListener);
        }
    },

    deleteSelectedVertex: function () {
        const sel = window._selectedDraftVertex;
        const layerId = window._currentDraftLayerId;
        if (!sel || !layerId || !window._draftVectorLayers || !window._draftVectorLayers[layerId]) {
            console.warn('[uspsim2d5] No draft vertex or point selected for deletion.');
            return false;
        }

        const draft = window._draftVectorLayers[layerId];
        const feat = sel.feature;
        const geom = feat.getGeometry();
        if (!geom) return false;

        const type = sel.geomType;

        if (type === 'Point') {
            draft.source.removeFeature(feat);
            console.log('[uspsim2d5] Deleted selected Point feature.');
        } else if (type === 'LineString') {
            const coords = geom.getCoordinates();
            if (coords.length - 1 < 2) {
                draft.source.removeFeature(feat);
                console.log('[uspsim2d5] LineString points < 2, removed entire line feature.');
            } else {
                coords.splice(sel.vertexIndex, 1);
                geom.setCoordinates(coords);
                console.log('[uspsim2d5] Deleted LineString vertex at index', sel.vertexIndex);
            }
        } else if (type === 'Polygon') {
            const rings = geom.getCoordinates();
            if (rings && rings[0]) {
                const ring = rings[0];
                const distinctCoords = ring.slice(0, ring.length - 1);

                if (distinctCoords.length - 1 < 3) {
                    draft.source.removeFeature(feat);
                    console.log('[uspsim2d5] Polygon distinct points < 3, removed entire polygon feature.');
                } else {
                    distinctCoords.splice(sel.vertexIndex, 1);
                    distinctCoords.push(distinctCoords[0]);
                    geom.setCoordinates([distinctCoords]);
                    console.log('[uspsim2d5] Deleted Polygon vertex at index', sel.vertexIndex);
                }
            }
        }

        window._selectedDraftVertex = null;
        draft.source.changed();
        return true;
    },

    stopInteractionOnly: function () {
        const map = window.activeOlMap;
        if (map) {
            if (window._drawInteraction) {
                map.removeInteraction(window._drawInteraction);
                window._drawInteraction = null;
            }
            if (window._modifyInteraction) {
                map.removeInteraction(window._modifyInteraction);
                window._modifyInteraction = null;
            }
            if (window._draftMapClickListener) {
                map.un('singleclick', window._draftMapClickListener);
                window._draftMapClickListener = null;
            }
        }
        window._drawRedoStack = [];
        window._activeSketchGeometry = null;
        window._selectedDraftVertex = null;
    },

    stopDrawing: function () {
        this.stopInteractionOnly();
        const map = window.activeOlMap;
        if (map && window._draftVectorLayers) {
            Object.keys(window._draftVectorLayers).forEach(function (id) {
                try {
                    map.removeLayer(window._draftVectorLayers[id].layer);
                } catch (e) { }
            });
        }
        window._draftVectorLayers = {};
        window._drawSource = null;
        window._currentDraftLayerId = null;
    },

    removeDraftLayer: function (layerId) {
        const map = window.activeOlMap;
        if (layerId === window._currentDraftLayerId) {
            this.stopInteractionOnly();
        }
        if (map && window._draftVectorLayers && window._draftVectorLayers[layerId]) {
            try {
                map.removeLayer(window._draftVectorLayers[layerId].layer);
            } catch (e) { }
            delete window._draftVectorLayers[layerId];
        }
    },

    undoDrawPoint: function () {
        const currentDraft = window._currentDraftLayerId ? window._draftVectorLayers[window._currentDraftLayerId] : null;

        let undoneSketch = false;
        if (window._drawInteraction && window._activeSketchGeometry) {
            try {
                const coords = window._activeSketchGeometry.getCoordinates();
                const ring = Array.isArray(coords[0]) ? coords[0] : coords;
                if (ring && ring.length > 2) {
                    window._drawInteraction.removeLastPoint();
                    undoneSketch = true;
                    console.log('[uspsim2d5] Undone last vertex in active sketch.');
                }
            } catch (e) { }
        }

        if (!undoneSketch && currentDraft) {
            const features = currentDraft.source.getFeatures();
            if (features && features.length > 0) {
                const lastFeature = currentDraft.undoStack.length > 0 ? currentDraft.undoStack.pop() : features[features.length - 1];
                if (lastFeature) {
                    try { currentDraft.source.removeFeature(lastFeature); } catch (e) { }
                    currentDraft.redoStack.push(lastFeature);
                    console.log('[uspsim2d5] Undone finalized feature/point placement.');
                }
            }
        }
    },

    redoDrawPoint: function () {
        const currentDraft = window._currentDraftLayerId ? window._draftVectorLayers[window._currentDraftLayerId] : null;

        if (currentDraft && currentDraft.redoStack && currentDraft.redoStack.length > 0) {
            const redoFeature = currentDraft.redoStack.pop();
            if (redoFeature) {
                currentDraft.source.addFeature(redoFeature);
                currentDraft.undoStack.push(redoFeature);
                console.log('[uspsim2d5] Redone finalized feature/point placement.');
                return;
            }
        }

        if (window._drawInteraction && window._drawRedoStack && window._drawRedoStack.length > 0) {
            try {
                const coord = window._drawRedoStack.pop();
                if (coord) {
                    window._drawInteraction.appendCoordinates([coord]);
                    console.log('[uspsim2d5] Redone sketch vertex point.');
                }
            } catch (e) {
                console.warn('[uspsim2d5] Unable to redo sketch point:', e);
            }
        }
    },

    getDrawnGeoJsonForLayer: function (layerId) {
        const map = window.activeOlMap;
        if (!map || !window._draftVectorLayers || !window._draftVectorLayers[layerId]) return null;
        const source = window._draftVectorLayers[layerId].source;
        const features = source.getFeatures();
        if (!features || features.length === 0) return null;
        const geojsonFormat = new ol.format.GeoJSON();
        return geojsonFormat.writeFeatures(features, {
            dataProjection: 'EPSG:4326',
            featureProjection: map.getView().getProjection()
        });
    },

    getDrawnGeoJson: function () {
        if (window._currentDraftLayerId) {
            return this.getDrawnGeoJsonForLayer(window._currentDraftLayerId);
        }
        return null;
    },

    loadDraftFeatureGeometry: function (layerId, geoJsonString) {
        const map = window.activeOlMap;
        if (!map || !geoJsonString || !window._draftVectorLayers || !window._draftVectorLayers[layerId]) return;

        try {
            const geojsonFormat = new ol.format.GeoJSON();
            const features = geojsonFormat.readFeatures(geoJsonString, {
                dataProjection: 'EPSG:4326',
                featureProjection: map.getView().getProjection()
            });

            const draftObj = window._draftVectorLayers[layerId];
            draftObj.source.clear();
            draftObj.source.addFeatures(features);
            features.forEach(function (f) {
                draftObj.undoStack.push(f);
            });
            map.render();
            console.log('[uspsim2d5] Loaded existing draft feature geometry into layer', layerId);
        } catch (err) {
            console.error('[uspsim2d5] Error loading draft feature geometry:', err);
        }
    },

    renderPlanFeatures: function (featuresPayloadList, fallbackColor) {
        this.clearPlanHighlight();
        const map = window.activeOlMap;
        if (!map || !featuresPayloadList) return;

        try {
            const geojsonFormat = new ol.format.GeoJSON();

            if (!window._highlightSource) {
                window._highlightSource = new ol.source.Vector();
                window._highlightLayer = new ol.layer.Vector({
                    source: window._highlightSource,
                    style: function (feature) {
                        const color = feature.get('_planColor') || fallbackColor || '#10b981';
                        const isPulsing = feature.get('_isPulsing');
                        const pulseScale = feature.get('_pulseScale') || 1.0;

                        if (isPulsing) {
                            return new ol.style.Style({
                                fill: new ol.style.Fill({ color: 'rgba(245, 158, 11, ' + (0.45 * pulseScale) + ')' }),
                                stroke: new ol.style.Stroke({
                                    color: '#f59e0b',
                                    width: Math.round(5 * pulseScale)
                                }),
                                image: new ol.style.Circle({
                                    radius: Math.round(9 * pulseScale),
                                    fill: new ol.style.Fill({ color: '#f59e0b' }),
                                    stroke: new ol.style.Stroke({ color: '#ffffff', width: 3 })
                                })
                            });
                        }

                        return new ol.style.Style({
                            fill: new ol.style.Fill({ color: 'rgba(16, 185, 129, 0.30)' }),
                            stroke: new ol.style.Stroke({ color: color, width: 4 }),
                            image: new ol.style.Circle({
                                radius: 8,
                                fill: new ol.style.Fill({ color: color }),
                                stroke: new ol.style.Stroke({ color: '#ffffff', width: 2 })
                            })
                        });
                    },
                    zIndex: 995
                });
                map.addLayer(window._highlightLayer);
            }

            window._highlightSource.clear();

            const allFeatures = [];
            if (typeof featuresPayloadList === 'string') {
                const features = geojsonFormat.readFeatures(featuresPayloadList, {
                    dataProjection: 'EPSG:4326',
                    featureProjection: map.getView().getProjection()
                });
                features.forEach(function (f) { allFeatures.push(f); });
            } else if (Array.isArray(featuresPayloadList)) {
                featuresPayloadList.forEach(function (item) {
                    if (item.geoJson) {
                        const feats = geojsonFormat.readFeatures(item.geoJson, {
                            dataProjection: 'EPSG:4326',
                            featureProjection: map.getView().getProjection()
                        });
                        feats.forEach(function (f) {
                            if (item.color) f.set('_planColor', item.color);
                            allFeatures.push(f);
                        });
                    }
                });
            }

            allFeatures.forEach(function (f) {
                f.set('_isPulsing', true);
                f.set('_pulseScale', 1.0);
            });
            window._highlightSource.addFeatures(allFeatures);

            // 2-second glowing pulse animation (2000ms)
            if (window._pulseTimer) {
                clearInterval(window._pulseTimer);
            }
            let elapsed = 0;
            const duration = 2000;
            const interval = 50;

            window._pulseTimer = setInterval(function () {
                elapsed += interval;
                const progress = elapsed / duration;
                // Sine wave pulse effect (2 pulses over 2 seconds)
                const pulseScale = 1.0 + 0.6 * Math.sin(progress * Math.PI * 4);

                allFeatures.forEach(function (f) {
                    f.set('_pulseScale', pulseScale);
                });
                if (window._highlightLayer) {
                    window._highlightLayer.changed();
                }

                if (elapsed >= duration) {
                    clearInterval(window._pulseTimer);
                    window._pulseTimer = null;
                    allFeatures.forEach(function (f) {
                        f.set('_isPulsing', false);
                        f.set('_pulseScale', 1.0);
                    });
                    if (window._highlightLayer) {
                        window._highlightLayer.changed();
                    }
                }
            }, interval);

            map.render();
        } catch (err) {
            console.error('[uspsim2d5] Error rendering plan features:', err);
        }
    },

    clearPlanHighlight: function () {
        if (window._pulseTimer) {
            clearInterval(window._pulseTimer);
            window._pulseTimer = null;
        }
        if (window._highlightSource) {
            window._highlightSource.clear();
        }
    },

    hexToRgba: function (hex, alpha) {
        try {
            hex = (hex || '#3b82f6').trim().replace('#', '');
            if (hex.length === 6) {
                const r = parseInt(hex.substring(0, 2), 16);
                const g = parseInt(hex.substring(2, 4), 16);
                const b = parseInt(hex.substring(4, 6), 16);
                return `rgba(${r}, ${g}, ${b}, ${alpha})`;
            }
        } catch (e) { }
        return `rgba(59, 130, 246, ${alpha})`;
    },

    createHatchPattern: function (hexColorList, alpha) {
        const canvas = document.createElement('canvas');
        const size = 32;
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        const numColors = hexColorList.length;
        const stripeWidth = size / numColors;
        const self = this;

        ctx.clearRect(0, 0, size, size);

        for (let i = 0; i < numColors; i++) {
            const color = self.hexToRgba(hexColorList[i], alpha || 0.45);
            ctx.fillStyle = color;

            ctx.save();
            ctx.beginPath();
            ctx.moveTo(-size + i * stripeWidth, 0);
            ctx.lineTo(-size + (i + 1) * stripeWidth, 0);
            ctx.lineTo(size * 2 + (i + 1) * stripeWidth, size * 3);
            ctx.lineTo(size * 2 + i * stripeWidth, size * 3);
            ctx.closePath();
            ctx.fill();
            ctx.restore();
        }

        return ctx.createPattern(canvas, 'repeat');
    },

    renderTeamAreas: function (teamsPayloadList) {
        const map = window.activeOlMap;
        if (!map) {
            window._pendingLayersToRender.push(function () {
                window.uspsim2d5.renderTeamAreas(teamsPayloadList);
            });
            return;
        }

        const geojsonFormat = new ol.format.GeoJSON();
        window._teamAreaGeometries = [];

        if (!window._teamAreasSource) {
            window._teamAreasSource = new ol.source.Vector();
            window._teamAreasLayer = new ol.layer.Vector({
                source: window._teamAreasSource,
                style: function (feature) {
                    const color = feature.get('_teamColor') || '#3b82f6';
                    const fillColor = feature.get('_fillColor') || 'rgba(59, 130, 246, 0.30)';
                    return new ol.style.Style({
                        fill: new ol.style.Fill({ color: fillColor }),
                        stroke: new ol.style.Stroke({ color: color, width: 3, lineDash: [6, 4] })
                    });
                },
                zIndex: 990
            });
            map.addLayer(window._teamAreasLayer);
        }

        window._teamAreasSource.clear();

        if (Array.isArray(teamsPayloadList)) {
            const teamGeomList = [];

            teamsPayloadList.forEach(function (team) {
                if (team.areaDefinition) {
                    try {
                        const feats = geojsonFormat.readFeatures(team.areaDefinition, {
                            dataProjection: 'EPSG:4326',
                            featureProjection: map.getView().getProjection()
                        });
                        const hexColor = team.color || '#3b82f6';

                        feats.forEach(function (f) {
                            f.set('_teamName', team.name);
                            f.set('_teamColor', hexColor);

                            window._teamAreaGeometries.push({
                                geometry: f.getGeometry(),
                                color: hexColor,
                                teamName: team.name
                            });

                            teamGeomList.push({
                                feature: f,
                                teamName: team.name,
                                color: hexColor,
                                geometry: f.getGeometry()
                            });
                        });
                    } catch (e) {
                        console.warn('[uspsim2d5] Error parsing team area GeoJSON:', e);
                    }
                }
            });

            // Detect overlapping polygons across different teams
            const self = window.uspsim2d5;
            teamGeomList.forEach(function (item) {
                const overlappingColors = [item.color];

                teamGeomList.forEach(function (other) {
                    if (other.teamName !== item.teamName && !overlappingColors.includes(other.color)) {
                        try {
                            if (item.geometry.intersectsExtent(other.geometry.getExtent())) {
                                overlappingColors.push(other.color);
                            }
                        } catch (e) { }
                    }
                });

                if (overlappingColors.length > 1) {
                    // Overlapping multi-team area: render alternating diagonal striped hatch pattern
                    const hatchPattern = self.createHatchPattern(overlappingColors, 0.45);
                    item.feature.set('_fillColor', hatchPattern);
                    item.feature.set('_teamColor', overlappingColors[0]);
                } else {
                    // Single team area: standard semi-transparent fill
                    item.feature.set('_fillColor', self.hexToRgba(item.color, 0.30));
                }

                window._teamAreasSource.addFeature(item.feature);
            });
        }
    },

    toggleTeamAreasVisibility: function (visible) {
        if (window._teamAreasLayer) {
            window._teamAreasLayer.setVisible(visible);
        }
    },

    refreshTeamAreas: function (sessionId) {
        const sid = sessionId || window._currentSessionId;
        if (sid) {
            fetch(`/api/teams/session/${sid}`)
                .then(r => r.json())
                .then(teams => {
                    window.uspsim2d5.renderTeamAreas(teams);
                })
                .catch(e => console.warn('[uspsim2d5] Error fetching team areas:', e));
        }
    }
};
