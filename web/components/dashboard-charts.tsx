type ChartCardProps = {
  title: string;
  meta: string;
  children: React.ReactNode;
  className?: string;
};

const teal = "#00d4aa";
const tealDark = "#007a5e";
const ink = "#0d0f14";
const line = "#26303d";
const muted = "#718096";
const text = "#e8edf3";

const revenue = [2480, 3120, 2860, 3980, 3520, 4760, 4286];
const revenueLabels = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];
const transactions = [26, 34, 29, 42, 38, 51, 44];
const products = [
  { name: "13kg LPG Cylinder", value: 86 },
  { name: "6kg LPG Cylinder", value: 71 },
  { name: "Gas Regulator", value: 54 },
  { name: "Hose Connector", value: 38 },
  { name: "Safety Valve", value: 25 }
];

export function ChartCard({ title, meta, children, className = "" }: ChartCardProps) {
  return (
    <section className={`glass-panel hud-grid chart-card rounded-2xl p-5 ${className}`}>
      <div className="mb-4 flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-white">{title}</h2>
          <p className="mt-1 text-[10px] uppercase tracking-[.16em] text-slate-500">{meta}</p>
        </div>
        <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-accent shadow-[0_0_12px_rgba(0,212,170,.8)]" />
      </div>
      {children}
    </section>
  );
}

export function RevenueLineChart() {
  const max = 5000;
  const points = revenue.map((value, index) => `${20 + index * 80},${154 - (value / max) * 120}`).join(" ");
  const area = `20,154 ${points} 500,154`;
  return (
    <ChartCard title="Revenue signal" meta="Last 7 days · demo telemetry">
      <svg className="h-52 w-full overflow-visible" viewBox="0 0 520 190" role="img" aria-label="Revenue trend line chart">
        <defs>
          <linearGradient id="revenue-fill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={teal} stopOpacity=".3" />
            <stop offset="100%" stopColor={teal} stopOpacity="0" />
          </linearGradient>
        </defs>
        {[34, 74, 114, 154].map((y) => <line key={y} x1="20" x2="500" y1={y} y2={y} stroke={line} strokeDasharray="2 7" />)}
        <polygon points={area} fill="url(#revenue-fill)" />
        <polyline points={points} fill="none" stroke={teal} strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
        {revenue.map((value, index) => {
          const x = 20 + index * 80;
          const y = 154 - (value / max) * 120;
          return <circle key={value} cx={x} cy={y} r="4" fill={ink} stroke={teal} strokeWidth="2" />;
        })}
        {revenueLabels.map((label, index) => <text key={label} x={20 + index * 80} y="178" fill={muted} fontSize="9" textAnchor="middle">{label}</text>)}
        <text x="20" y="20" fill={text} fontSize="14" fontWeight="600">£4,286</text>
        <text x="500" y="20" fill={teal} fontSize="10" textAnchor="end">+12.8%</text>
      </svg>
      <p className="mt-1 text-[10px] text-slate-500">Demo signal · connect the sales feed to replace sample telemetry.</p>
    </ChartCard>
  );
}

export function TransactionBarsChart() {
  const max = Math.max(...transactions);
  return (
    <ChartCard title="Transaction cadence" meta="Daily volume · demo telemetry">
      <svg className="h-52 w-full" viewBox="0 0 420 190" role="img" aria-label="Transaction volume bar chart">
        {[34, 74, 114, 154].map((y) => <line key={y} x1="18" x2="402" y1={y} y2={y} stroke={line} strokeDasharray="2 7" />)}
        {transactions.map((value, index) => {
          const x = 28 + index * 56;
          const height = (value / max) * 116;
          return (
            <g key={value}>
              <rect x={x} y={154 - height} width="30" height={height} rx="5" fill={index === transactions.length - 1 ? teal : tealDark} fillOpacity={index === transactions.length - 1 ? ".9" : ".62"} />
              <text x={x + 15} y="178" fill={muted} fontSize="9" textAnchor="middle">{revenueLabels[index]}</text>
            </g>
          );
        })}
        <text x="18" y="20" fill={text} fontSize="14" fontWeight="600">184</text>
        <text x="402" y="20" fill={teal} fontSize="10" textAnchor="end">+8.2%</text>
      </svg>
      <p className="mt-1 text-[10px] text-slate-500">No live transaction model is connected in Phase 1.</p>
    </ChartCard>
  );
}

export function PaymentDonutChart() {
  const segments = [
    { label: "Card", value: 48, color: teal },
    { label: "Cash", value: 34, color: tealDark },
    { label: "Account", value: 18, color: teal }
  ];
  let offset = 0;
  return (
    <ChartCard title="Payment split" meta="Share of demo revenue">
      <div className="flex items-center justify-center gap-5 py-3">
        <svg className="h-36 w-36 -rotate-90" viewBox="0 0 120 120" role="img" aria-label="Payment method donut chart">
          <circle cx="60" cy="60" r="43" fill="none" stroke={line} strokeWidth="14" />
          {segments.map((segment) => {
            const dash = `${segment.value} ${100 - segment.value}`;
            const currentOffset = offset;
            offset += segment.value;
            return <circle key={segment.label} cx="60" cy="60" r="43" fill="none" stroke={segment.color} strokeWidth="14" pathLength="100" strokeDasharray={dash} strokeDashoffset={-currentOffset} />;
          })}
          <circle cx="60" cy="60" r="30" fill={ink} />
        </svg>
        <div className="space-y-3">
          {segments.map((segment) => <div key={segment.label} className="flex items-center gap-2 text-xs"><span className="h-2 w-2 rounded-sm" style={{ backgroundColor: segment.color }} /><span className="text-slate-400">{segment.label}</span><span className="font-semibold text-white">{segment.value}%</span></div>)}
        </div>
      </div>
      <p className="text-center text-[10px] text-slate-500">Illustrative split · awaiting connected payment data.</p>
    </ChartCard>
  );
}

export function TopProductsChart() {
  const max = products[0].value;
  return (
    <ChartCard title="Top products" meta="Units moved · demo catalogue">
      <svg className="h-56 w-full" viewBox="0 0 560 210" role="img" aria-label="Top products horizontal bar chart">
        {products.map((product, index) => {
          const y = 16 + index * 37;
          const width = (product.value / max) * 300;
          return (
            <g key={product.name}>
              <text x="0" y={y + 12} fill={text} fontSize="10">{product.name}</text>
              <rect x="190" y={y} width="300" height="13" rx="6" fill={line} />
              <rect x="190" y={y} width={width} height="13" rx="6" fill={index === 0 ? teal : tealDark} fillOpacity={index === 0 ? ".9" : ".72"} />
              <text x={198 + width} y={y + 11} fill={text} fontSize="10">{product.value}</text>
            </g>
          );
        })}
      </svg>
      <p className="text-[10px] text-slate-500">Demo catalogue movement · no sales feed connected yet.</p>
    </ChartCard>
  );
}

export function StockHealthGauge() {
  return (
    <ChartCard title="Stock health" meta="Inventory readiness · demo catalogue">
      <div className="flex items-center gap-5">
        <svg className="h-36 w-36 shrink-0 -rotate-90" viewBox="0 0 120 120" role="img" aria-label="Stock health gauge">
          <circle cx="60" cy="60" r="45" fill="none" stroke={line} strokeWidth="12" />
          <circle cx="60" cy="60" r="45" fill="none" stroke={teal} strokeWidth="12" pathLength="100" strokeDasharray="78 22" strokeLinecap="round" />
          <circle cx="60" cy="60" r="31" fill={ink} />
          <text x="60" y="65" fill={text} fontSize="19" fontWeight="600" textAnchor="middle" transform="rotate(90 60 60)">78%</text>
        </svg>
        <div>
          <p className="text-lg font-semibold text-white">Healthy</p>
          <p className="mt-1 text-xs leading-5 text-slate-400">18 of 23 products above their reorder level.</p>
          <div className="mt-3 h-1.5 w-36 overflow-hidden rounded-full bg-line"><div className="h-full w-[78%] rounded-full bg-accent shadow-[0_0_12px_rgba(0,212,170,.65)]" /></div>
          <p className="mt-2 text-[10px] uppercase tracking-wider text-accent">5 reorder signals</p>
        </div>
      </div>
      <p className="mt-5 text-[10px] text-slate-500">Demo health score · connect inventory for live thresholds.</p>
    </ChartCard>
  );
}

export function MarginTrendKpi() {
  return (
    <ChartCard title="Margin intelligence" meta="Gross margin · demo telemetry">
      <div className="flex items-end justify-between">
        <div><p className="text-3xl font-semibold tracking-tight text-white">31.4%</p><p className="mt-1 text-xs font-semibold text-accent">+2.6% vs last week</p></div>
        <span className="rounded-md border border-accent/20 bg-accent/5 px-2 py-1 text-[10px] uppercase tracking-wider text-accent">On target</span>
      </div>
      <svg className="mt-5 h-24 w-full" viewBox="0 0 420 100" role="img" aria-label="Gross margin trend">
        <defs><linearGradient id="margin-fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={teal} stopOpacity=".24" /><stop offset="100%" stopColor={teal} stopOpacity="0" /></linearGradient></defs>
        <polygon points="0,88 0,70 70,62 140,70 210,45 280,53 350,32 420,22 420,88" fill="url(#margin-fill)" />
        <polyline points="0,70 70,62 140,70 210,45 280,53 350,32 420,22" fill="none" stroke={teal} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
        <line x1="0" x2="420" y1="88" y2="88" stroke={line} />
      </svg>
      <p className="text-[10px] text-slate-500">Demo KPI · margin will be calculated when cost prices are available.</p>
    </ChartCard>
  );
}
