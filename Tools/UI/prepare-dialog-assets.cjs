const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),assets=[];
async function save(name,body,w=64,h=64,border=[0,0,0,0]){
 const file=`Assets/Res/image/common/${name}.png`,target=path.join(root,file);
 const data=await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`)).png().toBuffer();
 fs.writeFileSync(target,data);assets.push({path:file,border,maxSize:128});
}
(async()=>{
 const line='fill="none" stroke="#76634C" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"';
 await save('icon_audio_music',`<g ${line}><path d="M26 43V13L51 8V37M26 19L51 14"/><ellipse cx="18" cy="45" rx="8" ry="6" fill="#E8BD68"/><ellipse cx="43" cy="39" rx="8" ry="6" fill="#E8BD68"/></g>`);
 await save('icon_audio_sound',`<g ${line}><path d="M8 26H18L31 15V49L18 38H8Z" fill="#D7E4BB"/><path d="M40 23Q50 32 40 41M47 16Q64 32 47 48"/></g>`);
 await save('icon_notice_info','<circle cx="32" cy="32" r="27" fill="#F8CE77" stroke="#A47B41" stroke-width="3"/><path d="M32 29V45" stroke="#493627" stroke-width="5" stroke-linecap="round"/><circle cx="32" cy="19" r="3" fill="#493627"/>');
 await save('icon_gift_box',`<g ${line}><rect x="10" y="29" width="44" height="27" rx="3" fill="#F7CE79"/><rect x="7" y="20" width="50" height="12" rx="3" fill="#E0E9C9"/><path d="M32 21V56M32 20C8 20 15 0 25 9L32 20C56 20 49 0 39 9Z"/></g>`);
 await save('switch_track_9slice','<rect x="1.5" y="1.5" width="125" height="61" rx="30.5" fill="white" stroke="#A79E87" stroke-width="3"/>',128,64,[32,30,32,30]);
 await save('switch_knob','<circle cx="30" cy="31" r="27" fill="#A9A087" opacity=".28"/><circle cx="30" cy="29" r="27" fill="#FFFCF0" stroke="#B9AD91" stroke-width="2"/>',60,60);
 fs.writeFileSync(path.join(root,'Tools/UI/dialogs-assets.json'),JSON.stringify({assets},null,2));
 console.log(JSON.stringify({assets:assets.length,bytes:assets.reduce((n,a)=>n+fs.statSync(path.join(root,a.path)).size,0)}));
})();
