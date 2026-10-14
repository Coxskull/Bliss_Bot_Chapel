import React, {useState} from 'react';
import {createRoot} from 'react-dom/client';
import {usd} from './format.js';
import './style.css';
const cases = [
 {id:'demo-bliss-001',module:'Bliss Bot',amount:'25000',reason:'Cancellation after capture',captured:true},
 {id:'demo-creator-001',module:'Creator Academy',amount:'12500',reason:'Aged unresolved dispatch',captured:false},
 {id:'demo-wedding-001',module:'Wedding Planner',amount:'9007199254740993',reason:'Uncaptured cancellation',captured:false},
];
function App(){
 const [filter,setFilter]=useState('All modules'),[selected,setSelected]=useState(cases[0].id),[notes,setNotes]=useState({}),[text,setText]=useState('');
 const visible=cases.filter(c=>filter==='All modules'||c.module===filter),item=visible.find(c=>c.id===selected)||visible[0];
 function add(e){e.preventDefault();const value=text.trim();if(!value||value.length>2000)return;setNotes(n=>({...n,[item.id]:[...(n[item.id]||[]),{text:value,time:new Date().toLocaleTimeString()}]}));setText('');}
 return <main><header><div className="mark">B</div><div><p className="eyebrow">BLISS • FINANCIAL OPERATIONS</p><h1>Evidence before action</h1></div><span className="badge">LOCAL MOCK</span></header>
 <aside className="notice"><strong>Stripe account unverified · owner decision required</strong><p>Synthetic data only. No payment connection. Notes grant no approval and release no hold.</p></aside>
 <div className="layout"><section aria-label="Review queue"><div className="section-title"><h2>Review queue</h2><span>{visible.length} cases</span></div><label>Module<select value={filter} onChange={e=>{setFilter(e.target.value);setText('');}}>{['All modules',...cases.map(c=>c.module)].map(m=><option key={m}>{m}</option>)}</select></label>
 {visible.map(c=><button className={'case '+(c.id===item.id?'active':'')} key={c.id} onClick={()=>{setSelected(c.id);setText('');}}><span className="eyebrow">{c.module}</span><strong>{usd(c.amount)}</strong><span>{c.reason}</span><small>{c.id}</small></button>)}</section>
 <section aria-label="Case evidence" className="detail"><span className="badge">REVIEW REQUIRED</span><h2>{item.reason}</h2><p>{item.module} · {item.id}</p><dl><div><dt>Frozen amount</dt><dd>{usd(item.amount)}</dd></div><div><dt>Capture</dt><dd>{item.captured?'Recorded — preserve original journal':'No capture recorded'}</dd></div><div><dt>Evidence version</dt><dd>Synthetic fixture v1</dd></div><div><dt>Hold</dt><dd>Retained</dd></div></dl>
 <div className="policy"><h3>Proposed review policy</h3><p>Two distinct scoped administrators review the same evidence. Owner review is pending. Any release requires a separate reviewed action.</p></div>
 <h3>Local demonstration notes</h3><p className="muted">In-memory only; reload clears notes. No authentication or persistent audit is demonstrated.</p><ul className="notes">{(notes[item.id]||[]).map((n,i)=><li key={i}><small>Demo operator · {n.time}</small><p>{n.text}</p></li>)}</ul>
 <form onSubmit={add}><label htmlFor="note">Evidence observation</label><textarea id="note" maxLength={2000} value={text} onChange={e=>setText(e.target.value)} placeholder="Record an observation. This cannot approve or release payment."/><div className="form-foot"><small>{text.length}/2000</small><button disabled={!text.trim()}>Add local note</button></div></form></section></div><footer>AGM-LAUNCH-010 · Development continues · No live processing</footer></main>;
}
createRoot(document.getElementById('root')).render(<App/>);
