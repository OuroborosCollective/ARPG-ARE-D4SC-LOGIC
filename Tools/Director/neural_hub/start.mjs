import {server} from './server.mjs';
const p=process.env.NEURAL_HUB_PORT;
if(process.env.NEURAL_HUB_LOCAL_ONLY!=='1' || typeof p!=='string' || !/^[0-9]{2,5}$/.test(p) ||
   Number(p)<1024 || Number(p)>65535)throw Error('INVALID_LOCAL_ONLY_CONFIGURATION');
const x=await server.listen(Number(p),{host:'127.0.0.1'});
process.stdout.write(JSON.stringify({
 status:'LISTENING_LOCAL_ONLY',endpoint:x.url,tools:'28 domain advisors plus director',
 evidence:'PROTOCOL_ONLY_NOT_UNITY_RUNTIME'
})+'\n');
