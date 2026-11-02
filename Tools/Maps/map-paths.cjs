// Keep these paths aligned with Assets/Editor/MapResourcePaths.cs.
const styles = 'Assets/Res/Map/Styles';
module.exports = {
  styles,
  sharedCartoon: `${styles}/SharedCartoon`,
  autumnAcademy: `${styles}/AutumnAcademy`,
  common: `${styles}/Common`,
  runtimeMaps: 'Assets/Resources/Map',
  bambooMap: stage => {
    if (!Number.isInteger(stage) || stage < 1 || stage > 8) throw new Error('Bamboo stage must be 1..8');
    return `Assets/Resources/Map/ZlMap${stage}.prefab`;
  },
};
