import { useNavigate } from "react-router-dom";
import {
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import TrendingUpIcon from "@mui/icons-material/TrendingUp";
import TrendingDownIcon from "@mui/icons-material/TrendingDown";
import SmartToyIcon from "@mui/icons-material/SmartToyOutlined";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useGet } from "../app/api";
import {
  ErrorAlert,
  Loading,
  PageHeader,
  SectionCard,
  StatusChip,
} from "../components/common";
import { ChartTooltip, Legend, useChart } from "../components/charts";
import { date, money, moneyCompact, monthLabel, num } from "../utils/format";
import { useAppSelector } from "../app/store";
import type { Dashboard, Kpi } from "../types";

// For these KPIs a decrease is good news.
const LOWER_IS_BETTER = new Set(["purchases"]);

function KpiTile({ k }: { k: Kpi }) {
  const up = (k.changePercent ?? 0) >= 0;
  const good = LOWER_IS_BETTER.has(k.key) ? !up : up;
  return (
    <Card>
      <CardContent>
        <Typography variant="body2" color="text.secondary">
          {k.label}
        </Typography>
        <Typography variant="h5" sx={{ mt: 0.5 }}>
          {k.format === "currency" ? money(k.value) : num(k.value)}
        </Typography>
        {k.changePercent !== null && k.changePercent !== undefined ? (
          <Stack
            direction="row"
            spacing={0.5}
            sx={{
              alignItems: "center",
              mt: 0.5,
              color: good ? "success.main" : "error.main",
            }}
          >
            {up ? (
              <TrendingUpIcon fontSize="small" />
            ) : (
              <TrendingDownIcon fontSize="small" />
            )}
            <Typography variant="caption" sx={{ fontWeight: 600 }}>
              {up ? "+" : ""}
              {k.changePercent.toFixed(1)}% vs same period last month
            </Typography>
          </Stack>
        ) : (
          <Typography variant="caption" color="text.secondary">
            Current position
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}

export default function DashboardPage() {
  const { data, isLoading, error } = useGet<Dashboard>("/dashboard");
  const user = useAppSelector((s) => s.auth.user);
  const c = useChart();
  const navigate = useNavigate();

  if (isLoading) return <Loading label="Loading dashboard…" />;
  if (!data) return <ErrorAlert error={error} />;

  const axis = {
    stroke: c.axis,
    tick: { fill: c.muted, fontSize: 12 },
    tickLine: false,
  };

  return (
    <>
      <PageHeader
        title={`Welcome back, ${user?.fullName.split(" ")[0] ?? ""}`}
        subtitle="Sales, purchases, profit and stock at a glance"
        actions={
          <Button
            variant="contained"
            startIcon={<SmartToyIcon />}
            onClick={() => navigate("/ai/assistant")}
          >
            Ask the AI assistant
          </Button>
        }
      />

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "1fr 1fr",
            lg: "repeat(4, 1fr)",
          },
          gap: 2,
          mb: 2,
        }}
      >
        {data.kpis.map((k) => (
          <KpiTile key={k.key} k={k} />
        ))}
      </Box>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", lg: "2fr 1fr" },
          gap: 2,
          mb: 2,
        }}
      >
        <SectionCard title="Net sales & profit — last 12 months">
          <Legend
            items={[
              { label: "Net sales", color: c.series[0] },
              { label: "Net profit", color: c.series[1] },
              { label: "Purchases", color: c.series[2] },
            ]}
          />
          <ResponsiveContainer width="100%" height={290}>
            <LineChart
              data={data.trend}
              margin={{ top: 8, right: 12, left: 0, bottom: 0 }}
            >
              <CartesianGrid vertical={false} stroke={c.grid} />
              <XAxis dataKey="month" tickFormatter={monthLabel} {...axis} />
              <YAxis
                tickFormatter={(v) => moneyCompact(v)}
                {...axis}
                axisLine={false}
                width={70}
              />
              <Tooltip
                content={
                  <ChartTooltip format={money} labelFormat={monthLabel} />
                }
                cursor={{ stroke: c.axis }}
              />
              <Line
                type="monotone"
                dataKey="sales"
                name="Net sales"
                stroke={c.series[0]}
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 5, strokeWidth: 2, stroke: c.surface }}
              />
              <Line
                type="monotone"
                dataKey="profit"
                name="Net profit"
                stroke={c.series[1]}
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 5, strokeWidth: 2, stroke: c.surface }}
              />
              <Line
                type="monotone"
                dataKey="purchases"
                name="Purchases"
                stroke={c.series[2]}
                strokeWidth={2}
                dot={false}
                activeDot={{ r: 5, strokeWidth: 2, stroke: c.surface }}
              />
            </LineChart>
          </ResponsiveContainer>
        </SectionCard>

        <SectionCard title="Sales by category (this year)">
          <ResponsiveContainer width="100%" height={320}>
            <BarChart
              data={data.salesByCategory}
              layout="vertical"
              margin={{ top: 0, right: 16, left: 0, bottom: 0 }}
              barCategoryGap={6}
            >
              <CartesianGrid horizontal={false} stroke={c.grid} />
              <XAxis
                type="number"
                tickFormatter={(v) => moneyCompact(v)}
                {...axis}
              />
              <YAxis
                type="category"
                dataKey="name"
                width={130}
                {...axis}
                axisLine={false}
              />
              <Tooltip
                content={<ChartTooltip format={money} />}
                cursor={{ fill: c.grid, opacity: 0.4 }}
              />
              <Bar
                dataKey="amount"
                name="Sales"
                fill={c.series[0]}
                radius={[0, 4, 4, 0]}
                maxBarSize={22}
              />
            </BarChart>
          </ResponsiveContainer>
        </SectionCard>
      </Box>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "1fr 1fr", xl: "1fr 1fr 1fr" },
          gap: 2,
          mb: 2,
        }}
      >
        <SectionCard title="Top products (last 3 months)">
          <ResponsiveContainer width="100%" height={260}>
            <BarChart
              data={data.topProducts}
              layout="vertical"
              margin={{ top: 0, right: 16, left: 0, bottom: 0 }}
              barCategoryGap={6}
            >
              <XAxis type="number" hide />
              <YAxis
                type="category"
                dataKey="name"
                width={150}
                {...axis}
                axisLine={false}
                tickFormatter={(v: string) =>
                  v.length > 22 ? v.slice(0, 21) + "…" : v
                }
              />
              <Tooltip
                content={<ChartTooltip format={money} />}
                cursor={{ fill: c.grid, opacity: 0.4 }}
              />
              <Bar
                dataKey="amount"
                name="Sales"
                fill={c.series[0]}
                radius={[0, 4, 4, 0]}
                maxBarSize={20}
              />
            </BarChart>
          </ResponsiveContainer>
        </SectionCard>

        <SectionCard
          title="Receivables aging"
          action={
            <Button size="small" onClick={() => navigate("/invoices")}>
              View invoices
            </Button>
          }
        >
          <ResponsiveContainer width="100%" height={260}>
            <BarChart
              data={data.receivablesAging}
              margin={{ top: 8, right: 8, left: 0, bottom: 0 }}
            >
              <CartesianGrid vertical={false} stroke={c.grid} />
              <XAxis dataKey="bucket" {...axis} />
              <YAxis
                tickFormatter={(v) => moneyCompact(v)}
                {...axis}
                axisLine={false}
                width={64}
              />
              <Tooltip
                content={<ChartTooltip format={money} />}
                cursor={{ fill: c.grid, opacity: 0.4 }}
              />
              <Bar
                dataKey="amount"
                name="Outstanding"
                fill={c.series[0]}
                radius={[4, 4, 0, 0]}
                maxBarSize={44}
              />
            </BarChart>
          </ResponsiveContainer>
        </SectionCard>

        <SectionCard
          title="Low-stock alerts"
          action={
            <Button
              size="small"
              onClick={() => navigate("/ai/insights?tab=reorder")}
            >
              Smart reorder
            </Button>
          }
        >
          {data.lowStock.length === 0 ? (
            <Typography color="text.secondary">
              All products are above reorder level.
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Product</TableCell>
                  <TableCell align="right">On hand</TableCell>
                  <TableCell align="right">Reorder at</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.lowStock.map((p) => (
                  <TableRow key={p.productId}>
                    <TableCell>
                      <Typography variant="body2" noWrap sx={{ maxWidth: 190 }}>
                        {p.name}
                      </Typography>
                    </TableCell>
                    <TableCell
                      align="right"
                      sx={{
                        color: p.quantityOnHand <= 0 ? "error.main" : undefined,
                        fontWeight: 600,
                      }}
                    >
                      {num(p.quantityOnHand)}
                    </TableCell>
                    <TableCell align="right">{num(p.reorderLevel)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </SectionCard>
      </Box>

      <SectionCard
        title="Recent sales orders"
        action={
          <Button size="small" onClick={() => navigate("/sales-orders")}>
            All orders
          </Button>
        }
      >
        <Box sx={{ overflowX: "auto" }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Order</TableCell>
                <TableCell>Date</TableCell>
                <TableCell>Customer</TableCell>
                <TableCell>Status</TableCell>
                <TableCell align="right">Total</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.recentOrders.map((o) => (
                <TableRow
                  key={o.id}
                  hover
                  sx={{ cursor: "pointer" }}
                  onClick={() => navigate(`/sales-orders/${o.id}`)}
                >
                  <TableCell>{o.orderNumber}</TableCell>
                  <TableCell>{date(o.orderDate)}</TableCell>
                  <TableCell>{o.customerName}</TableCell>
                  <TableCell>
                    <StatusChip status={o.status} />
                  </TableCell>
                  <TableCell align="right">{money(o.totalAmount)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
      </SectionCard>
    </>
  );
}
