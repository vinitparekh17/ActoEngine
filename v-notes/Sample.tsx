// import React, { useState, useEffect } from 'react';
// import { 
//   Menu, X, ChevronRight, ChevronDown, Check, Terminal, 
//   Zap, Shield, Database, Layout, Code2, Cpu, MessageSquare,
//   Search, GitBranch, Globe, ArrowRight, Star, Settings, Play
// } from 'lucide-react';

// /* --- Components --- */

// const Navbar = () => {
//   const [isOpen, setIsOpen] = useState(false);
//   const [scrolled, setScrolled] = useState(false);

//   useEffect(() => {
//     const handleScroll = () => setScrolled(window.scrollY > 20);
//     window.addEventListener('scroll', handleScroll);
//     return () => window.removeEventListener('scroll', handleScroll);
//   }, []);

//   return (
//     <nav className={`fixed w-full z-50 transition-all duration-300 ${scrolled ? 'bg-black/80 backdrop-blur-md border-b border-white/10' : 'bg-transparent'}`}>
//       <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
//         <div className="flex justify-between items-center h-20">
//           <div className="flex-shrink-0 flex items-center gap-2 cursor-pointer">
//             <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center transform rotate-3">
//               <Zap className="text-white w-5 h-5" />
//             </div>
//             <span className="text-white font-bold text-xl tracking-tight">Neuron</span>
//           </div>
          
//           <div className="hidden md:flex items-center space-x-8">
//             {['Solutions', 'Docs', 'Pricing', 'Resources', 'Company'].map((item) => (
//               <a key={item} href="#" className="text-gray-300 hover:text-white text-sm font-medium transition-colors">
//                 {item}
//               </a>
//             ))}
//           </div>

//           <div className="hidden md:flex items-center space-x-4">
//             <a href="#" className="text-gray-300 hover:text-white text-sm font-medium transition-colors">Sign In</a>
//             <button className="bg-blue-600 hover:bg-blue-500 text-white px-5 py-2.5 rounded-full text-sm font-medium transition-all transform hover:scale-105 shadow-[0_0_20px_rgba(37,99,235,0.3)]">
//               Try it free
//             </button>
//           </div>

//           <div className="md:hidden flex items-center">
//             <button onClick={() => setIsOpen(!isOpen)} className="text-gray-300 hover:text-white">
//               {isOpen ? <X size={24} /> : <Menu size={24} />}
//             </button>
//           </div>
//         </div>
//       </div>

//       {/* Mobile menu */}
//       {isOpen && (
//         <div className="md:hidden bg-black/95 backdrop-blur-lg border-b border-white/10 absolute w-full">
//           <div className="px-4 pt-2 pb-8 space-y-4">
//             {['Solutions', 'Docs', 'Pricing', 'Resources', 'Company'].map((item) => (
//               <a key={item} href="#" className="block text-gray-300 hover:text-white text-base font-medium py-2">
//                 {item}
//               </a>
//             ))}
//             <div className="pt-4 flex flex-col gap-3">
//               <a href="#" className="text-center text-gray-300 hover:text-white py-2">Sign In</a>
//               <button className="w-full bg-blue-600 text-white px-5 py-3 rounded-lg font-medium">
//                 Try it free
//               </button>
//             </div>
//           </div>
//         </div>
//       )}
//     </nav>
//   );
// };

// const MockCodeEditor = () => (
//   <div className="rounded-xl overflow-hidden border border-white/10 bg-[#0F1117] shadow-2xl relative z-10">
//     {/* Editor Header */}
//     <div className="flex items-center justify-between px-4 py-3 bg-[#1A1D24] border-b border-white/5">
//       <div className="flex space-x-2">
//         <div className="w-3 h-3 rounded-full bg-red-500/80"></div>
//         <div className="w-3 h-3 rounded-full bg-yellow-500/80"></div>
//         <div className="w-3 h-3 rounded-full bg-green-500/80"></div>
//       </div>
//       <div className="flex space-x-4 text-xs text-gray-400 font-mono">
//         <span className="text-blue-400">Main.tsx</span>
//         <span>App.tsx</span>
//         <span>utils.ts</span>
//       </div>
//       <div className="w-16"></div> {/* Spacer */}
//     </div>

//     {/* Editor Content Split */}
//     <div className="grid grid-cols-1 md:grid-cols-2 min-h-[400px]">
//       {/* Code Side */}
//       <div className="p-6 font-mono text-xs sm:text-sm overflow-hidden border-r border-white/5 bg-[#0F1117]">
//         <div className="space-y-1 text-gray-300 whitespace-pre-wrap">
//           <div className="flex"><span className="w-6 text-gray-600 select-none">1</span><span className="text-purple-400">interface</span> <span className="text-yellow-300 ml-2">UserProfile</span> <span className="text-gray-400">{'{'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">2</span><span className="ml-4 text-blue-300">name</span>: <span className="text-green-400">string</span>;</div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">3</span><span className="ml-4 text-blue-300">email</span>: <span className="text-green-400">string</span>;</div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">4</span><span className="ml-4 text-blue-300">posts</span>: <span className="text-green-400">string</span>[];</div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">5</span><span className="text-gray-400">{'}'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">6</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">7</span><span className="text-purple-400">export</span> <span className="text-purple-400">const</span> <span className="text-yellow-300">getUserProfile</span> = <span className="text-purple-400">async</span> (id: <span className="text-green-400">string</span>) ={'>'} <span className="text-gray-400">{'{'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">8</span><span className="ml-4 text-purple-400">try</span> <span className="text-gray-400">{'{'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">9</span><span className="ml-8 text-purple-400">const</span> user = <span className="text-purple-400">await</span> fetchUser(id);</div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">10</span><span className="ml-8 text-purple-400">const</span> posts = <span className="text-purple-400">await</span> fetchPosts(id);</div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">11</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">12</span><span className="ml-8 text-purple-400">if</span> (!user) <span className="text-gray-400">{'{'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">13</span><span className="ml-12 text-purple-400">throw</span> <span className="text-purple-400">new</span> <span className="text-yellow-300">Error</span>(<span className="text-orange-300">'User not found'</span>);</div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">14</span><span className="ml-8 text-gray-400">{'}'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">15</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">16</span><span className="ml-8 text-purple-400">return</span> <span className="text-gray-400">{'{'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">17</span><span className="ml-12">...user,</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">18</span><span className="ml-12">posts: posts.map(p ={'>'} p.title)</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">19</span><span className="ml-8 text-gray-400">{'}'}</span>;</div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">20</span><span className="ml-4 text-gray-400">{'}'}</span> <span className="text-purple-400">catch</span> (error) <span className="text-gray-400">{'{'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">21</span><span className="ml-8">console.error(error);</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">22</span><span className="ml-4 text-gray-400">{'}'}</span></div>
//           <div className="flex"><span className="w-6 text-gray-600 select-none">23</span><span className="text-gray-400">{'}'}</span>;</div>
//         </div>
//       </div>

//       {/* AI Chat Side */}
//       <div className="flex flex-col bg-[#13161C] relative">
//         <div className="p-4 border-b border-white/5 flex items-center justify-between">
//           <div className="flex items-center gap-2">
//             <div className="w-2 h-2 rounded-full bg-blue-500 animate-pulse"></div>
//             <span className="text-xs font-semibold text-gray-300 uppercase tracking-wider">Neuron AI</span>
//           </div>
//           <div className="text-[10px] text-gray-500 bg-white/5 px-2 py-1 rounded">GPT-4o</div>
//         </div>

//         <div className="flex-1 p-4 space-y-4 overflow-y-auto">
//           {/* User Message */}
//           <div className="flex justify-end">
//             <div className="bg-[#2A2D36] text-gray-200 text-xs sm:text-sm py-2 px-3 rounded-l-lg rounded-tr-lg max-w-[90%]">
//               Explain what this function does?
//             </div>
//           </div>

//           {/* AI Response */}
//           <div className="flex justify-start">
//             <div className="bg-blue-600/10 border border-blue-500/20 text-gray-200 text-xs sm:text-sm p-3 rounded-r-lg rounded-tl-lg max-w-[95%] space-y-2">
//               <p className="font-semibold text-blue-300 mb-1">Analysis for <code className="bg-black/30 px-1 rounded text-xs">getUserProfile</code>:</p>
//               <ul className="list-disc pl-4 space-y-1 text-gray-300">
//                 <li>Fetches user data and their associated posts in parallel using <code className="text-orange-300">await</code>.</li>
//                 <li>Validates user existence, throwing an error if null.</li>
//                 <li>Merges user data with a mapped array of post titles.</li>
//               </ul>
//               <div className="mt-3 pt-3 border-t border-white/10">
//                 <p className="text-xs text-gray-400 mb-2">Suggestion:</p>
//                 <div className="bg-black/30 p-2 rounded text-xs font-mono text-green-300">
//                   // Consider adding try-catch error boundaries for network timeouts
//                 </div>
//               </div>
//             </div>
//           </div>
//         </div>

//         {/* Input Area */}
//         <div className="p-3 mt-auto border-t border-white/5">
//           <div className="relative">
//             <input 
//               type="text" 
//               placeholder="Ask Neuron to refactor..." 
//               className="w-full bg-[#1A1D24] border border-white/10 rounded-lg py-2 pl-3 pr-10 text-xs text-white focus:outline-none focus:border-blue-500/50"
//             />
//             <button className="absolute right-2 top-1.5 text-blue-500 hover:text-blue-400">
//               <ArrowRight size={14} />
//             </button>
//           </div>
//         </div>
//       </div>
//     </div>
//   </div>
// );

// const FeatureCard = ({ icon: Icon, title, description, delay }) => (
//   <div className={`flex items-start gap-4 p-4 rounded-xl hover:bg-white/5 transition-colors cursor-default animate-fade-in-up`} style={{ animationDelay: `${delay}ms` }}>
//     <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-blue-900/20 flex items-center justify-center text-blue-400">
//       <Icon size={20} />
//     </div>
//     <div>
//       <h3 className="text-white font-medium mb-1">{title}</h3>
//       <p className="text-gray-400 text-sm leading-relaxed">{description}</p>
//     </div>
//   </div>
// );

// const LargeFeature = ({ title, description, children, align = 'left' }) => (
//   <div className="grid md:grid-cols-2 gap-12 items-center mb-32">
//     <div className={`space-y-6 ${align === 'right' ? 'md:order-2' : ''}`}>
//       <h3 className="text-3xl md:text-4xl font-bold text-white leading-tight">{title}</h3>
//       <p className="text-lg text-gray-400 leading-relaxed">{description}</p>
//       <ul className="space-y-3">
//         {['Real-time analysis', 'Context-aware suggestions', 'Secure & Private'].map((item) => (
//           <li key={item} className="flex items-center text-gray-300">
//             <div className="w-5 h-5 rounded-full bg-blue-500/20 flex items-center justify-center mr-3">
//               <Check size={12} className="text-blue-400" />
//             </div>
//             {item}
//           </li>
//         ))}
//       </ul>
//     </div>
//     <div className={`relative ${align === 'right' ? 'md:order-1' : ''}`}>
//       <div className="absolute inset-0 bg-blue-500/20 blur-[100px] rounded-full"></div>
//       <div className="relative bg-[#0F1117] border border-white/10 rounded-2xl p-6 shadow-2xl overflow-hidden group hover:border-blue-500/30 transition-colors">
//         {children}
//       </div>
//     </div>
//   </div>
// );

// const AccordionItem = ({ question, answer, isOpen, onClick }) => (
//   <div className="border-b border-white/10">
//     <button 
//       className="w-full py-6 flex items-center justify-between text-left focus:outline-none group"
//       onClick={onClick}
//     >
//       <span className={`text-lg font-medium transition-colors ${isOpen ? 'text-blue-400' : 'text-gray-200 group-hover:text-white'}`}>
//         {question}
//       </span>
//       <span className={`transform transition-transform duration-300 ${isOpen ? 'rotate-180 text-blue-400' : 'text-gray-500'}`}>
//         <ChevronDown size={20} />
//       </span>
//     </button>
//     <div className={`overflow-hidden transition-all duration-300 ease-in-out ${isOpen ? 'max-h-48 opacity-100 mb-6' : 'max-h-0 opacity-0'}`}>
//       <p className="text-gray-400 leading-relaxed">
//         {answer}
//       </p>
//     </div>
//   </div>
// );

// const Footer = () => (
//   <footer className="bg-black border-t border-white/10 pt-20 pb-10 relative overflow-hidden">
//     <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10">
//       <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-5 gap-8 mb-16">
//         <div className="col-span-2 lg:col-span-2 pr-8">
//           <div className="flex items-center gap-2 mb-6">
//             <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center transform rotate-3">
//               <Zap className="text-white w-5 h-5" />
//             </div>
//             <span className="text-white font-bold text-xl">Neuron</span>
//           </div>
//           <p className="text-gray-400 text-sm leading-relaxed mb-6 max-w-sm">
//             Neuron works alongside you, generating clean code, catching errors, and accelerating your workflow from idea to deploy.
//           </p>
//           <div className="flex space-x-4">
//             {[1, 2, 3, 4].map(i => (
//               <div key={i} className="w-8 h-8 rounded-full bg-white/5 hover:bg-white/10 flex items-center justify-center cursor-pointer transition-colors text-gray-400 hover:text-white">
//                 <Globe size={14} />
//               </div>
//             ))}
//           </div>
//         </div>
        
//         {[
//           { title: "Product", links: ["Features", "Integrations", "Pricing", "Changelog"] },
//           { title: "Company", links: ["About Us", "Careers", "Blog", "Contact"] },
//           { title: "Resources", links: ["Documentation", "API Reference", "Community", "Help Center"] }
//         ].map((column) => (
//           <div key={column.title}>
//             <h4 className="text-white font-bold mb-6">{column.title}</h4>
//             <ul className="space-y-4">
//               {column.links.map((link) => (
//                 <li key={link}>
//                   <a href="#" className="text-gray-400 hover:text-blue-400 text-sm transition-colors">{link}</a>
//                 </li>
//               ))}
//             </ul>
//           </div>
//         ))}
//       </div>
      
//       <div className="border-t border-white/10 pt-8 flex flex-col md:flex-row justify-between items-center gap-4">
//         <p className="text-gray-500 text-sm">© 2025 Neuron Inc. All rights reserved.</p>
//         <div className="flex gap-6">
//           <a href="#" className="text-gray-500 hover:text-gray-300 text-sm">Privacy Policy</a>
//           <a href="#" className="text-gray-500 hover:text-gray-300 text-sm">Terms of Service</a>
//           <a href="#" className="text-gray-500 hover:text-gray-300 text-sm">Cookie Settings</a>
//         </div>
//       </div>
//     </div>
    
//     {/* Giant Watermark */}
//     <div className="absolute bottom-0 left-1/2 transform -translate-x-1/2 translate-y-1/3 select-none pointer-events-none">
//       <span className="text-[15vw] font-bold text-white/[0.03] tracking-tighter leading-none">
//         NEURON
//       </span>
//     </div>
//   </footer>
// );

// /* --- Main Application --- */

// export default function App() {
//   const [openFaq, setOpenFaq] = useState(0);

//   return (
//     <div className="min-h-screen bg-black text-gray-100 font-sans selection:bg-blue-500/30">
//       <Navbar />

//       {/* Hero Section */}
//       <section className="relative pt-32 pb-20 lg:pt-48 lg:pb-32 overflow-hidden">
//         {/* Ambient Background Glow */}
//         <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[1000px] h-[600px] bg-blue-600/20 rounded-full blur-[120px] -z-10 opacity-50"></div>
//         <div className="absolute bottom-0 right-0 w-[800px] h-[600px] bg-purple-600/10 rounded-full blur-[100px] -z-10 opacity-30"></div>

//         <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
//           <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white/5 border border-white/10 text-xs font-medium text-blue-300 mb-8 backdrop-blur-sm animate-fade-in">
//             <span className="relative flex h-2 w-2">
//               <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-blue-400 opacity-75"></span>
//               <span className="relative inline-flex rounded-full h-2 w-2 bg-blue-500"></span>
//             </span>
//             Neuron 2.0 is now available
//           </div>
          
//           <h1 className="text-5xl md:text-7xl font-extrabold tracking-tight text-white mb-6 leading-[1.1]">
//             Build and ship faster<br />
//             <span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-400 to-purple-500">with your AI partner</span>
//           </h1>
          
//           <p className="max-w-2xl mx-auto text-lg md:text-xl text-gray-400 mb-10 leading-relaxed">
//             Neuron works alongside you, generating clean code, catching errors, and accelerating your workflow from idea to deploy.
//           </p>
          
//           <div className="flex flex-col sm:flex-row items-center justify-center gap-4 mb-20">
//             <button className="w-full sm:w-auto px-8 py-4 bg-blue-600 hover:bg-blue-500 text-white rounded-full font-semibold text-lg transition-all shadow-[0_0_40px_rgba(37,99,235,0.4)] hover:shadow-[0_0_60px_rgba(37,99,235,0.6)] transform hover:-translate-y-1">
//               Get started for free
//             </button>
//             <button className="w-full sm:w-auto px-8 py-4 bg-white/5 hover:bg-white/10 text-white border border-white/10 rounded-full font-semibold text-lg transition-all backdrop-blur-sm flex items-center justify-center gap-2 group">
//               <Play size={18} className="group-hover:text-blue-400 transition-colors" /> Watch Demo
//             </button>
//           </div>

//           {/* Hero Mockup */}
//           <div className="relative mx-auto max-w-5xl animate-fade-in-up">
//             <div className="absolute -inset-1 bg-gradient-to-r from-blue-500 to-purple-600 rounded-2xl blur opacity-20"></div>
//             <MockCodeEditor />
//           </div>

//           {/* Social Proof */}
//           <div className="mt-20 pt-10 border-t border-white/5">
//             <p className="text-sm text-gray-500 mb-8 font-medium">TRUSTED BY ENGINEERS AT</p>
//             <div className="flex flex-wrap justify-center gap-8 md:gap-16 opacity-50 grayscale hover:grayscale-0 transition-all duration-500">
//               {['OdeaoLabs', 'Kintsugi', 'Stacked Lab', 'Magnolia', 'Warpspeed'].map((brand) => (
//                 <div key={brand} className="flex items-center gap-2 text-lg font-bold text-white">
//                   <div className="w-6 h-6 bg-white rounded-full"></div> {brand}
//                 </div>
//               ))}
//             </div>
//           </div>
//         </div>
//       </section>

//       {/* Features Grid Section */}
//       <section className="py-24 relative bg-black">
//         <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
//           <div className="grid lg:grid-cols-2 gap-16 items-start">
//             <div className="sticky top-32">
//               <h2 className="text-4xl md:text-5xl font-bold text-white mb-6 leading-tight">
//                 Your AI coding<br />
//                 <span className="text-blue-500">partner for...</span>
//               </h2>
//               <p className="text-gray-400 text-lg mb-8">
//                 Neuron isn't just a chatbot. It's deeply integrated into your development environment to handle complex tasks.
//               </p>
//               <button className="flex items-center text-blue-400 font-semibold hover:text-blue-300 transition-colors group">
//                 View all features <ArrowRight className="ml-2 group-hover:translate-x-1 transition-transform" size={20} />
//               </button>
//             </div>
            
//             <div className="grid sm:grid-cols-1 gap-4">
//               <FeatureCard icon={Database} title="Database optimization" description="Analyze slow queries and generate optimized indexes instantly." delay={0} />
//               <FeatureCard icon={Zap} title="Performance tuning" description="Identify bottlenecks in your React renders or Node.js event loop." delay={100} />
//               <FeatureCard icon={Code2} title="Complex refactors" description="Rename variables and restructure components across thousands of files safely." delay={200} />
//               <FeatureCard icon={Shield} title="Code reviews" description="Automated security checks and style guide enforcement before you push." delay={300} />
//               <FeatureCard icon={Search} title="Debugging" description="Paste a stack trace and get the exact line and fix suggestions." delay={400} />
//               <FeatureCard icon={Layout} title="Documentation" description="Generate beautiful markdown docs from your codebase automatically." delay={500} />
//               <FeatureCard icon={Globe} title="Framework migrations" description="Moving from Vue to React? Neuron handles the boilerplate translation." delay={600} />
//             </div>
//           </div>
//         </div>
//       </section>

//       {/* Deep Dive Features */}
//       <section className="py-32 relative overflow-hidden bg-[#050608]">
//         <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
//           <div className="text-center mb-24">
//             <h2 className="text-3xl md:text-5xl font-bold text-white mb-6">Why Developers Choose Neuron</h2>
//             <p className="text-gray-400 max-w-2xl mx-auto">Built by developers, for developers. We solved the hardest parts of AI integration so you don't have to.</p>
//           </div>

//           <LargeFeature 
//             title="Understand every line you write" 
//             description="Neuron learns how your code fits together. It understands your functions, your structure, and your style, so it can help you reason through problems like a real teammate."
//           >
//              {/* Visual representation of code understanding */}
//              <div className="space-y-4">
//                <div className="bg-[#1A1D24] p-4 rounded-lg border border-white/5 shadow-inner">
//                  <div className="flex items-center justify-between mb-2">
//                    <span className="text-xs text-blue-400 font-mono">auth.ts</span>
//                    <span className="text-xs text-green-400 bg-green-900/20 px-2 py-0.5 rounded">Analyzed</span>
//                  </div>
//                  <div className="space-y-2">
//                    <div className="h-2 bg-gray-700/50 rounded w-3/4"></div>
//                    <div className="h-2 bg-gray-700/50 rounded w-1/2"></div>
//                    <div className="h-2 bg-blue-500/30 rounded w-full animate-pulse"></div>
//                    <div className="h-2 bg-gray-700/50 rounded w-2/3"></div>
//                  </div>
//                </div>
//                <div className="bg-[#252830] p-3 rounded-lg border border-blue-500/20 ml-8 relative">
//                  <div className="absolute -left-2 top-4 w-2 h-2 bg-[#252830] transform rotate-45 border-l border-b border-blue-500/20"></div>
//                  <p className="text-xs text-gray-300">"This function handles JWT validation but is missing an expiry check. Should I add it?"</p>
//                </div>
//              </div>
//           </LargeFeature>

//           <LargeFeature 
//             title="Switch models based on the task" 
//             description="No more 'one model fits all.' Use the right AI for the right task. Use fast models for autocomplete and reasoning models for architecture."
//             align="right"
//           >
//             <div className="flex flex-col items-center justify-center h-64 bg-[#0F1117]">
//               <div className="w-full max-w-sm bg-[#1A1D24] rounded-lg border border-white/10 overflow-hidden shadow-2xl">
//                 <div className="p-3 border-b border-white/5 text-xs text-gray-500 font-medium">MODEL SELECTOR</div>
//                 <div className="p-2 space-y-1">
//                   {['GPT-4o', 'Claude 3.5 Sonnet', 'Gemini 1.5 Pro', 'Llama 3 70B'].map((model, i) => (
//                     <div key={model} className={`flex items-center justify-between p-3 rounded cursor-pointer ${i === 0 ? 'bg-blue-600/20 border border-blue-500/30' : 'hover:bg-white/5'}`}>
//                       <div className="flex items-center gap-3">
//                         <div className={`w-2 h-2 rounded-full ${i === 0 ? 'bg-blue-400 shadow-[0_0_8px_rgba(96,165,250,0.8)]' : 'bg-gray-600'}`}></div>
//                         <span className={`text-sm ${i === 0 ? 'text-white font-medium' : 'text-gray-400'}`}>{model}</span>
//                       </div>
//                       {i === 0 && <Check size={14} className="text-blue-400" />}
//                     </div>
//                   ))}
//                 </div>
//               </div>
//             </div>
//           </LargeFeature>
          
//            <LargeFeature 
//             title="Catch bugs before they hit production" 
//             description="Neuron analyzes your changes for edge cases, performance issues, and silent failures, long before they reach production."
//           >
//              <div className="relative pt-6 pl-6 pb-6 pr-2 bg-[#1A1D24] rounded-lg border border-red-500/20">
//                <div className="absolute top-0 right-0 bg-red-500/10 text-red-400 text-[10px] px-2 py-1 rounded-bl-lg border-l border-b border-red-500/20 font-bold uppercase tracking-wider">High Severity</div>
//                <div className="font-mono text-sm space-y-1">
//                  <div className="text-gray-500">14  <span className="text-purple-400">function</span> <span className="text-yellow-300">processPayment</span>(amount) {'{'}</div>
//                  <div className="text-gray-500 relative">
//                    15    <span className="text-purple-400">if</span> (amount <span className="text-red-400 font-bold">!=</span> null) {'{'}
//                    <div className="absolute left-0 bottom-0 w-full h-[1px] bg-red-500/50"></div>
//                  </div>
//                  <div className="text-gray-500">16      <span className="text-gray-400">// ... processing logic</span></div>
//                  <div className="text-gray-500">17    {'}'}</div>
//                  <div className="text-gray-500">18  {'}'}</div>
//                </div>
               
//                <div className="mt-4 bg-red-900/10 border border-red-500/30 rounded p-3 flex gap-3">
//                  <div className="mt-0.5 text-red-400"><Shield size={16} /></div>
//                  <div>
//                    <p className="text-xs font-bold text-red-300 mb-1">Type Safety Error</p>
//                    <p className="text-xs text-gray-400">Using loose equality (!=) with null can lead to unexpected behavior with 0 or undefined. Use strict equality (!==).</p>
//                    <div className="mt-2 flex gap-2">
//                      <button className="text-[10px] bg-red-500/20 hover:bg-red-500/30 text-red-300 px-2 py-1 rounded transition-colors">Apply Fix</button>
//                      <button className="text-[10px] text-gray-500 hover:text-gray-300 px-2 py-1">Ignore</button>
//                    </div>
//                  </div>
//                </div>
//              </div>
//           </LargeFeature>

//         </div>
//       </section>

//       {/* Integrations */}
//       <section className="py-20 bg-black text-center">
//         <div className="max-w-7xl mx-auto px-4">
//           <h3 className="text-2xl text-gray-400 mb-12">Available on your favorite tools...</h3>
//           <div className="flex flex-wrap justify-center gap-6">
//             {['VS Code', 'IntelliJ', 'GitHub', 'GitLab', 'Neovim', 'Terminal'].map((tool) => (
//               <div key={tool} className="w-16 h-16 md:w-20 md:h-20 bg-[#111] rounded-2xl flex items-center justify-center border border-white/5 hover:border-blue-500/50 hover:bg-[#151515] transition-all cursor-pointer group hover:-translate-y-2 shadow-lg">
//                 {/* Placeholder Icons using Lucide/Text as I can't load real logos easily */}
//                 <div className="text-gray-600 group-hover:text-white transition-colors font-bold text-xs md:text-sm">
//                   {tool === 'VS Code' && <Code2 size={32} />}
//                   {tool === 'IntelliJ' && <span className="font-serif text-xl">Ij</span>}
//                   {tool === 'GitHub' && <GitBranch size={32} />}
//                   {tool === 'GitLab' && <span className="text-orange-500"><GitBranch size={32} /></span>}
//                   {tool === 'Neovim' && <Terminal size={32} />}
//                   {tool === 'Terminal' && <MessageSquare size={32} />}
//                 </div>
//               </div>
//             ))}
//           </div>
//         </div>
//       </section>

//       {/* FAQ */}
//       <section className="py-24 bg-[#050608]">
//         <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8">
//           <h2 className="text-3xl font-bold text-white mb-12 text-center">Frequently Asked Questions</h2>
//           <div className="space-y-2">
//             {[
//               { q: "What exactly is Neuron?", a: "Neuron is an AI-powered coding partner that integrates directly into your IDE. It helps you write, debug, and optimize code by understanding your entire project context, not just the file you're working on." },
//               { q: "Does Neuron replace developers?", a: "No. Neuron is designed to be a force multiplier for developers. It handles the repetitive, tedious parts of coding (like writing boilerplate, documentation, and tests) so you can focus on architecture and problem-solving." },
//               { q: "Which languages does Neuron support?", a: "Neuron supports over 30 major programming languages, with deep specialized support for JavaScript/TypeScript, Python, Go, Rust, Java, and C++." },
//               { q: "Is my code secure?", a: "Absolutely. We offer an on-premise solution for enterprise clients, and our cloud solution is SOC2 Type II compliant. Your code is never used to train our public models without your explicit permission." },
//             ].map((item, i) => (
//               <AccordionItem 
//                 key={i} 
//                 question={item.q} 
//                 answer={item.a} 
//                 isOpen={openFaq === i} 
//                 onClick={() => setOpenFaq(openFaq === i ? -1 : i)} 
//               />
//             ))}
//           </div>
//           <div className="mt-12 text-center">
//             <p className="text-gray-400 mb-4">Have more questions?</p>
//             <button className="bg-white/5 hover:bg-white/10 text-white px-6 py-2 rounded-lg text-sm font-medium transition-colors border border-white/10">
//               Contact Us
//             </button>
//           </div>
//         </div>
//       </section>

//       {/* Bottom CTA */}
//       <section className="py-24 relative overflow-hidden">
//         <div className="absolute inset-0 bg-blue-900/10"></div>
//         <div className="absolute bottom-0 left-0 w-full h-1/2 bg-gradient-to-t from-blue-900/20 to-transparent"></div>
        
//         <div className="max-w-4xl mx-auto px-4 text-center relative z-10">
//           <h2 className="text-4xl md:text-6xl font-bold text-white mb-8 tracking-tight">
//             Ship better code <br/> with Neuron
//           </h2>
//           <button className="bg-blue-600 hover:bg-blue-500 text-white px-10 py-4 rounded-full font-bold text-lg shadow-[0_0_50px_rgba(37,99,235,0.5)] transition-all hover:scale-105">
//             Get started for free
//           </button>
//         </div>
//       </section>

//       <Footer />
//     </div>
//   );
// }