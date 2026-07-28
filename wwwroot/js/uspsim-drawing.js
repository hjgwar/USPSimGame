window.uspsim2d5 = window.uspsim2d5 || {};

window.uspsim2d5.renderInfrastructureLayer = function (geoJsonInput, targetKey) {
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
                strokeColor = '#059669';
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
        const initialVisible = window._desiredVisibilities[layerKey] ?? false;

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
                console.log(`[Infrastructure Log] SUCCESS: Added Electrical Network layer '${layerKey}' (${features.length} features).`);
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
};

window.uspsim2d5.startDrawing = function (geomType, strokeColor, fillColor, layerId) {
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
};

window.uspsim2d5.stopInteractionOnly = function () {
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
};

window.uspsim2d5.stopDrawing = function () {
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
};

window.uspsim2d5.toggleDrawingActive = function (active) {
    if (window._drawInteraction) window._drawInteraction.setActive(active);
    if (window._modifyInteraction) window._modifyInteraction.setActive(active);
};

window.uspsim2d5.undoDrawPoint = function () {
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
};

window.uspsim2d5.redoDrawPoint = function () {
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
};

window.uspsim2d5.deleteSelectedVertex = function () {
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
};

window.uspsim2d5.getDrawnGeoJsonForLayer = function (layerId) {
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
};

window.uspsim2d5.getDrawnGeoJson = function () {
    if (window._currentDraftLayerId) {
        return this.getDrawnGeoJsonForLayer(window._currentDraftLayerId);
    }
    return null;
};

window.uspsim2d5.loadDraftFeatureGeometry = function (layerId, geoJsonString) {
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
};

window.uspsim2d5.removeDraftLayer = function (layerId) {
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
};
