export function usd(minor) {
  if (!/^\d+$/.test(minor)) throw new Error('Invalid exact minor units');
  const amount = BigInt(minor);
  return `$${(amount / 100n).toLocaleString('en-US')}.${(amount % 100n).toString().padStart(2, '0')} USD`;
}
