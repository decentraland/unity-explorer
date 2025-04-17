These sub-graphs require the Stylized Water 2 render feature to be active and the "Displacement Prepass" functionality enabled on it.

If so, the water geometry's height (including any displacement effects) is rendered into a buffer.

This allows other shaders to sample the water's height information this way. May be used for various effects.

* Note: Only water geometry on the "Water" layer is rendered into this buffer!
* Height data is represented per-pixel, so discrepancies between this data and the actual height of a vertex is to be expected.