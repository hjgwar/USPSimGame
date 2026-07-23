SELECT g."Id", g."GameSessionId", m."Key", m."Name", g."IsEnabled", LENGTH(g."CachedDataContent") AS content_length, LEFT(g."CachedDataContent", 150) AS sample 
FROM "GameSessionMapLayers" g 
JOIN "MapLayerDefinitions" m ON g."MapLayerDefinitionId" = m."Id" 
WHERE g."GameSessionId" = 9;
