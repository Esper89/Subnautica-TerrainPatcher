#!/usr/bin/env rust-script

use std::{
    alloc,
    array,
    collections::VecDeque,
    fs::File,
    io::{self, BufWriter, Write},
    path::Path,
    process::ExitCode,
};

fn main() -> ExitCode {
    let generate_previews = || -> io::Result<()> {
        generate_preview("material-preview-sn.optoctreepatch", [12, 18, 12], material_valid_sn)?;
        generate_preview("material-preview-bz.optoctreepatch", [11, 18, 12], material_valid_bz)?;
        Ok(())
    };

    match generate_previews() {
        Ok(()) => ExitCode::SUCCESS,
        Err(err) => { eprintln!("i/o error: {err}"); ExitCode::FAILURE },
    }
}

fn generate_preview(
    path: impl AsRef<Path>,
    batch: [i16; 3],
    material_valid: impl FnMut(u8) -> bool,
) -> io::Result<()> {
    let grid = generate_grid(material_valid);

    let mut output = BufWriter::new(File::create(path)?);

    let [[x_a, x_b], [y_a, y_b], [z_a, z_b]] = batch.map(|n| n.to_le_bytes());
    output.write_all(&[0, 0, 0, 0, x_a, x_b, y_a, y_b, z_a, z_b, 125])?;

    for i in 0..125 {
        let octree = generate_octree(&grid, i);

        let [count_a, count_b] = (1 + octree.count_children() as u16).to_le_bytes();
        output.write_all(&[i as u8, count_a, count_b])?;

        let mut idx: u16 = 1;
        octree.traverse_bf(|node| {
            let [idx_a, idx_b] = if node.children.is_some() {
                let bytes = idx.to_le_bytes();
                idx += 8; bytes
            } else { [0; 2] };

            let [voxel_a, voxel_b] = node.voxel.to_bytes();

            output.write_all(&[voxel_a, voxel_b, idx_a, idx_b])
        })?;
    }

    Ok(())
}

fn generate_grid(mut material_valid: impl FnMut(u8) -> bool) -> Grid {
    let mut grid = Grid::new();

    for i in 1..255 {
        let material = i as u8;
        if material_valid(material) {
            let i_x = i % 16;
            let i_z = 15 - i / 16;

            let [x, y, z] = [
                11 + 9 * i_x + if i_x >= 8 { 3 } else { 0 },
                80,
                11 + 9 * i_z + if i_z >= 8 { 3 } else { 0 },
            ];

            draw_sphere(&mut grid, [x, y, z], 3.5, material);
        }
    }

    grid
}

fn draw_sphere(grid: &mut Grid, [x, y, z]: [usize; 3], radius: f64, material: u8) {
    let half_width = radius.abs().ceil() as usize;

    let aabb_start = [x - half_width, y - half_width, z - half_width];
    let aabb_end = [x + half_width, y + half_width, z + half_width];

    let [cx, cy, cz] = [x as f64, y as f64, z as f64];

    grid.set_field(aabb_start, aabb_end, |[x, y, z]| {
        let [x, y, z] = [x as f64 - cx, y as f64 - cy, z as f64 - cz];
        let distance = (x * x + y * y + z * z).sqrt() - radius;
        Voxel::new(material, distance)
    });
}

fn generate_octree(grid: &Grid, idx: usize) -> Node {
    fn generate_node(grid: &Grid, [x, y, z]: [usize; 3], size: usize) -> Node {
        if size == 0 { Node { voxel: grid.get([x, y, z]), children: None } }
        else {
            let children = array::from_fn(|i| {
                let size = size - 1;
                let [x, y, z] = [
                    x + (1 << size) * (i >> 2 & 1),
                    y + (1 << size) * (i >> 1 & 1),
                    z + (1 << size) * (i >> 0 & 1),
                ];

                generate_node(grid, [x, y, z], size)
            });

            let base = children[0].voxel;
            let homogenenous = children.iter().skip(1).all(|child| child.voxel == base);
            let subchildren = children.iter().any(|child| child.children.is_some());

            if homogenenous && !subchildren { Node { voxel: base, children: None } }
            else {
                let material = most_used_material(&children);

                let distance = children
                    .iter()
                    .map(|child| child.voxel.distance())
                    .sum::<f64>() / 8.0;

                Node { voxel: Voxel::new(material, distance), children: Some(Box::new(children)) }
            }
        }
    }

    let [x, y, z] = [idx / (5 * 5), idx % (5 * 5) / 5, idx % 5];
    let [x, y, z] = [x * 32, y * 32, z * 32];

    generate_node(grid, [x, y, z], 5)
}

fn most_used_material(children: &[Node; 8]) -> u8 {
    let mut material_counts: [(u8, u8); 8] = [(0, 0); 8];
    for child in children {
        let material = child.voxel.material;
        if material != 0 {
            for (id, count) in &mut material_counts {
                if *id == material { *count += 1; break }
                else if *id == 0 { *id = material; *count = 1; break }
            }
        }
    }

    material_counts
        .into_iter()
        .take_while(|(id, _)| *id != 0)
        .min_by(|(_, lhs), (_, rhs)| lhs.cmp(rhs).reverse())
        .map(|(id, _)| id)
        .unwrap_or(0)
}

#[derive(Copy, Clone, Eq, PartialEq)]
#[repr(C)]
struct Voxel {
    material: u8,
    distance: u8,
}

struct Grid(Box<[Voxel; 160 * 160 * 160]>);

struct Node {
    voxel: Voxel,
    children: Option<Box<[Node; 8]>>,
}

impl Voxel {
    fn new(material: u8, distance: f64) -> Voxel {
        let distance = (126.0 - (distance * 126.0).min(126.0).max(-127.0)).round() as u8;
        Voxel { material, distance }
    }

    fn distance(&self) -> f64 { (126.0 - self.distance as f64) / 126.0 }

    fn to_bytes(&self) -> [u8; 2] {
        let (material, mut distance) = (self.material, self.distance);

        if material != 0 && distance == 0 { distance = 1; }
        if distance == 253 { distance = 0; }

        [material, distance]
    }
}

impl Grid {
    fn new() -> Grid {
        // We manually allocate the grid on the heap (instead of using `Box::new`) to avoid
        // overflowing the stack. `Voxel` is `repr(C)` and is valid when zero-initialized.

        let layout = alloc::Layout::new::<[Voxel; 160 * 160 * 160]>();

        // SAFETY: `layout` does not have a size of zero. The pointer is not null if it is passed to
        // `Box::from_raw`. The data pointed to was initialized by `alloc::alloc_zeroed`. All-zero
        // is a valid bit pattern for `Voxel`.
        let boxed = unsafe {
            let ptr = alloc::alloc_zeroed(layout);

            if ptr.is_null() { alloc::handle_alloc_error(layout) }
            else { Box::from_raw(ptr.cast::<[Voxel; 160 * 160 * 160]>()) }
        };

        Grid(boxed)
    }

    #[track_caller]
    fn get(&self, [x, y, z]: [usize; 3]) -> Voxel {
        if x < 160 && y < 160 && z < 160 { self.0[Grid::idx([x, y, z])] }
        else { panic!("index [{x}, {y}, {z}] out of bounds of grid with size [160; 3]") }
    }

    #[track_caller]
    fn set(&mut self, [x, y, z]: [usize; 3], mut value: Voxel) {
        // We ensure that all completely empty voxels in the grid also have no material.
        if value.distance == 0 { value.material = 0; }

        if x < 160 && y < 160 && z < 160 { self.0[Grid::idx([x, y, z])] = value; }
        else { panic!("index [{x}, {y}, {z}] out of bounds of grid with size [160; 3]") }
    }

    #[track_caller]
    fn set_field(
        &mut self,
        [start_x, start_y, start_z]: [usize; 3],
        [end_x, end_y, end_z]: [usize; 3],
        mut value: impl FnMut([usize; 3]) -> Voxel,
    ) {
        if start_x < 160 && start_y < 160 && start_z < 160
            && end_x <= 160 && end_y <= 160 && end_z <= 160 {
            for x in start_x..end_x {
                for y in start_y..end_y {
                    for z in start_z..end_z {
                        self.set([x, y, z], value([x, y, z]));
                    }
                }
            }
        } else {
            panic!(
                "range [{start_x}, {start_y}, {start_z}]–[{end_x}, {end_y}, {end_z}] out of bounds \
                of grid with size [160; 3]"
            )
        }
    }

    fn idx([x, y, z]: [usize; 3]) -> usize { x * (160 * 160) + y * 160 + z }
}

impl Node {
    fn count_children(&self) -> usize {
        if let Some(children) = &self.children {
            8 + children.iter().map(Node::count_children).sum::<usize>()
        } else { 0 }
    }

    fn traverse_bf<E>(&self, mut f: impl FnMut(&Node) -> Result<(), E>) -> Result<(), E> {
        let mut q = VecDeque::new();
        q.push_back(self);

        while let Some(node) = q.pop_front() {
            f(node)?;

            if let Some(children) = &node.children {
                for child in children.iter() { q.push_back(child); }
            }
        }

        Ok(())
    }
}

fn material_valid_sn(material: u8) -> bool {
    match material {
        // Assigned
        1..=94 | 97..=106 | 109..=116 | 120 | 124..=133 | 140..=167 | 170..=176 | 180..=254 => true,

        // Unassigned
        95 | 96 | 107 | 108 | 117..=119 | 121..=123 | 134..=139 | 168 | 169 | 177..=179 => false,

        // Invalid
        0 | 255 => false,
    }
}

fn material_valid_bz(material: u8) -> bool {
    match material {
        // Assigned
        1 | 85..=93 | 100..=107 | 109..=114 | 121..=139 | 150..=155 | 159..=180 | 182..=185 |
        200..=202 | 205..=214 | 217..=241 | 250 => true,

        // Unassigned
        2..=84 | 94..=99 | 108 | 115..=120 | 140..=149 | 156..=158 | 181 | 186..=199 | 203 | 204 |
        215 | 216 | 242..=249 | 251..=254 => false,

        // Invalid
        0 | 255 => false,
    }
}
