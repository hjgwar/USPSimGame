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
    }
};
