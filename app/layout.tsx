import type { Metadata } from 'next';
import './globals.css';
export const metadata: Metadata = {title:'Mesa · Restaurante',description:'Tu salón, los pedidos y la carta, en un mismo lugar.',icons:{icon:'/favicon.svg'}};
export default function Layout({children}:{children:React.ReactNode}){return <html lang="es-AR"><body>{children}</body></html>}
