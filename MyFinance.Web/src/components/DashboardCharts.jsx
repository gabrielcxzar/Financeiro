import React, { useMemo } from 'react';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
} from 'chart.js';
import { Bar } from 'react-chartjs-2';

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend);

const modernMinimalistColors = [
  '#0F172A', // Slate 900
  '#334155', // Slate 700
  '#64748B', // Slate 500
  '#10B981', // Emerald
  '#F43F5E', // Rose
  '#F59E0B', // Amber
  '#2563EB', // Blue
  '#8B5CF6', // Purple
];

export default function DashboardCharts({ categorySummary = [], compact = false }) {
  const chartData = useMemo(() => {
    return {
      labels: categorySummary.map((item) => item.name),
      datasets: [
        {
          label: 'Despesas (R$)',
          data: categorySummary.map((item) => item.total),
          backgroundColor: categorySummary.map((_, i) => modernMinimalistColors[i % modernMinimalistColors.length]),
          borderRadius: 6,
          borderSkipped: false,
          maxBarThickness: 40,
        },
      ],
    };
  }, [categorySummary]);

  const options = useMemo(
    () => ({
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { display: false },
        tooltip: {
          backgroundColor: '#0F172A',
          titleFont: { family: 'Plus Jakarta Sans', size: 12 },
          bodyFont: { family: 'Inter', size: 12 },
          padding: 10,
          cornerRadius: 8,
          callbacks: {
            label: (context) => ` R$ ${Number(context.raw || 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`,
          },
        },
      },
      scales: {
        y: {
          beginAtZero: true,
          grid: {
            color: '#F1F5F9',
          },
          ticks: {
            font: { family: 'Inter', size: 11 },
            color: '#94A3B8',
            callback: (value) => `R$ ${value >= 1000 ? `${(value / 1000).toFixed(0)}k` : value}`,
          },
        },
        x: {
          grid: { display: false },
          ticks: {
            font: { family: 'Plus Jakarta Sans', size: 12 },
            color: '#64748B',
            autoSkip: true,
            maxRotation: compact ? 35 : 0,
            minRotation: compact ? 25 : 0,
          },
        },
      },
    }),
    [compact],
  );

  return (
    <div style={{ height: compact ? 260 : 300, width: '100%' }}>
      <Bar options={options} data={chartData} />
    </div>
  );
}
